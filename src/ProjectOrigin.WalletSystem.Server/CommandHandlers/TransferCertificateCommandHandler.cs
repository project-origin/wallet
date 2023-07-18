using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Net.Client;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Options;

namespace ProjectOrigin.WalletSystem.Server.CommandHandlers;

public record TransferCertificateCommand(string Owner, string Registry, Guid CertificateId, uint Quantity, Guid Receiver);

public class TransferCertificateCommandHandler : IConsumer<TransferCertificateCommand>
{
    private readonly UnitOfWork _unitOfWork;
    private readonly ILogger<TransferCertificateCommandHandler> _logger;
    private readonly IOptions<RegistryOptions> _registryOptions;

    public TransferCertificateCommandHandler(UnitOfWork unitOfWork, ILogger<TransferCertificateCommandHandler> logger, IOptions<RegistryOptions> registryOptions)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _registryOptions = registryOptions;
    }

    public async Task Consume(ConsumeContext<TransferCertificateCommand> context)
    {
        var msg = context.Message;

        var slice = await _unitOfWork.CertificateRepository.GetAvailableSlice(msg.Registry, msg.CertificateId);
        if (slice == null)
        {
            _logger.LogError("Trying to transfer non-available slice. Msg: {msg}", msg);
            return;
        }

        await _unitOfWork.CertificateRepository.SetSliceState(slice, SliceState.Slicing);
        _unitOfWork.Commit();

        var wallet = await _unitOfWork.WalletRepository.GetWalletByOwner(msg.Owner);
        if (wallet == null)
        {
            _logger.LogError("Sender does not own a wallet. Owner: {owner}", msg.Owner);
            return;
        }

        var receiverDepositEndpoint = await _unitOfWork.WalletRepository.GetReceiverDepositEndpoint(msg.Receiver);
        if (receiverDepositEndpoint == null)
        {
            _logger.LogError("A receiver deposit endpoint was not found for this transfer. Receiver Id: " + msg.Receiver);
            return;
        }

        //This part is probably wrong but I need a next position in the receiver wallet
        var receiversDepositEndpoint = await _unitOfWork.WalletRepository.GetDepositEndpointFromPublicKey(receiverDepositEndpoint.PublicKey);
        var receiversNextPosition = await _unitOfWork.WalletRepository.GetNextWalletPosition(receiversDepositEndpoint!.WalletId!.Value);

        var registry = await _unitOfWork.RegistryRepository.GetRegistryFromName(msg.Registry);
        if (registry == null)
        {
            _logger.LogError("Unknown registry {registry}", msg.Registry);
            return;
        }

        var registryUrl = _registryOptions.Value.RegistryUrls[msg.Registry];
        if (registryUrl == null)
        {
            _logger.LogError("Registry not configured to have a url. Registry: {registry}", msg.Registry);
            return;
        }

        using var channel = GrpcChannel.ForAddress(registryUrl);
        var client = new RegistryService.RegistryServiceClient(channel);

        //Not sure what to set RandomR to
        var slice1 = new Slice(Guid.NewGuid(), receiverDepositEndpoint.Id, receiversNextPosition, registry.Id, slice.CertificateId, msg.Quantity, slice.RandomR, SliceState.Registering);
        await _unitOfWork.CertificateRepository.InsertSlice(slice1);
        _unitOfWork.Commit();
        if (msg.Quantity == slice.Quantity)
        {
            await TransferInRegistry(client, slice1, msg.Registry, receiverDepositEndpoint.PublicKey);
            
            await _unitOfWork.CertificateRepository.SetSliceState(slice, SliceState.Sliced);
            await _unitOfWork.CertificateRepository.SetSliceState(slice1, SliceState.Available);
            _unitOfWork.Commit();

            //TODO how to call receivedSlice on receiver wallet?
        }
        else if (msg.Quantity < slice.Quantity)
        {
            //TODO: GetNextWalletPosition needs to be scalable. Two instances can get the same position
            //Not sure if I should create a DepositEndpoint here but I lack one
            var nextPosition = await _unitOfWork.WalletRepository.GetNextWalletPosition(wallet.Id);
            var depositEndpoint = new DepositEndpoint(Guid.NewGuid(), wallet.Id, nextPosition, wallet.PrivateKey.Derive(nextPosition).Neuter(), msg.Owner, "", "");
            await _unitOfWork.WalletRepository.CreateDepositEndpoint(depositEndpoint);

            //Not sure what to set RandomR to
            var slice2 = new Slice(Guid.NewGuid(), depositEndpoint.Id, nextPosition, registry.Id, slice.CertificateId, slice.Quantity - msg.Quantity, slice.RandomR, SliceState.Registering);
            await _unitOfWork.CertificateRepository.InsertSlice(slice2);
            _unitOfWork.Commit();

            await SliceInRegistry(client, slice1, slice2, msg.Registry, receiverDepositEndpoint.PublicKey);

            await _unitOfWork.CertificateRepository.SetSliceState(slice, SliceState.Sliced);
            await _unitOfWork.CertificateRepository.SetSliceState(slice1, SliceState.Available);
            await _unitOfWork.CertificateRepository.SetSliceState(slice2, SliceState.Available);
            _unitOfWork.Commit();
            //TODO how to call receivedSlice on receiver wallet?
        }
    }

    private async Task SliceInRegistry(RegistryService.RegistryServiceClient client, Slice slice1, Slice slice2, string registry, IHDPublicKey receiverDepositEndpointPublicKey)
    {
        var id = new ProjectOrigin.Common.V1.FederatedStreamId
        {
            Registry = registry,
            StreamId = new ProjectOrigin.Common.V1.Uuid { Value = slice1.CertificateId.ToString() }
        };

        var slicedEvent = new SlicedEvent
        {
            CertificateId = id,
            //SumProof = Dunno what to set this to
            //SourceSliceHash = Dunno what to set this to
        };

        var commitment1 = new SecretCommitmentInfo((uint)slice1.Quantity);
        var poSlice1 = new SlicedEvent.Types.Slice
        {
            NewOwner = new PublicKey
            {
                Type = KeyType.Secp256K1,
                Content = ByteString.CopyFrom(receiverDepositEndpointPublicKey.Export())
            },
            Quantity = new ProjectOrigin.Electricity.V1.Commitment
            {
                Content = ByteString.CopyFrom(commitment1.Commitment.C),
                RangeProof = ByteString.CopyFrom(commitment1.CreateRangeProof(id.StreamId.Value))
            }
        };

        var commitment2 = new SecretCommitmentInfo((uint)slice2.Quantity);
        var poSlice2 = new SlicedEvent.Types.Slice
        {
            NewOwner = new PublicKey
            {
                Type = KeyType.Secp256K1,
                Content = ByteString.CopyFrom(receiverDepositEndpointPublicKey.Export())
            },
            Quantity = new ProjectOrigin.Electricity.V1.Commitment
            {
                Content = ByteString.CopyFrom(commitment2.Commitment.C),
                RangeProof = ByteString.CopyFrom(commitment2.CreateRangeProof(id.StreamId.Value))
            }
        };

        slicedEvent.NewSlices.Add(poSlice1);
        slicedEvent.NewSlices.Add(poSlice2);

        var header = new TransactionHeader
        {
            FederatedStreamId = id,
            PayloadType = SlicedEvent.Descriptor.FullName,
            PayloadSha512 = ByteString.CopyFrom(SHA512.HashData(slicedEvent.ToByteArray())),
            Nonce = Guid.NewGuid().ToString(),
        };
        var headerSignature = _registryOptions.Value.Dk1IssuerKey.Sign(header.ToByteArray()).ToArray();
        var transaction = new Transaction
        {
            Header = header,
            HeaderSignature = ByteString.CopyFrom(headerSignature),
            Payload = slicedEvent.ToByteString()
        };
        var request = new SendTransactionsRequest();
        request.Transactions.Add(transaction);

        await client.SendTransactionsAsync(request);

        var statusRequest = new GetTransactionStatusRequest
        {
            Id = Convert.ToBase64String(SHA256.HashData(transaction.ToByteArray()))
        };

        var began = DateTime.Now;
        while (true)
        {
            var status = await client.GetTransactionStatusAsync(statusRequest);

            if (status.Status == TransactionState.Committed)
                break;
            else if (status.Status == TransactionState.Failed)
                throw new Exception($"Failed to slice certificate. Message: {status.Message}");
            else
                await Task.Delay(1000);

            if (DateTime.Now - began > TimeSpan.FromMinutes(1))
                throw new Exception("Timed out waiting for transaction to commit");
        }
    }

    private async Task TransferInRegistry(RegistryService.RegistryServiceClient client, Slice slice, string registry, IHDPublicKey receiverDepositEndpointPublicKey)
    {
        var id = new ProjectOrigin.Common.V1.FederatedStreamId
        {
            Registry = registry,
            StreamId = new ProjectOrigin.Common.V1.Uuid { Value = slice.CertificateId.ToString() }
        };

        var transferredEvent = new TransferredEvent
        {
            CertificateId = id,
            NewOwner = new PublicKey
            {
                Content = ByteString.CopyFrom(receiverDepositEndpointPublicKey.Export()),
                Type = KeyType.Secp256K1
            },
            //SourceSliceHash = Dunno what to set this to
        };
        var header = new TransactionHeader
        {
            FederatedStreamId = id,
            PayloadType = TransferredEvent.Descriptor.FullName,
            PayloadSha512 = ByteString.CopyFrom(SHA512.HashData(transferredEvent.ToByteArray())),
            Nonce = Guid.NewGuid().ToString(),
        };
        var headerSignature = _registryOptions.Value.Dk1IssuerKey.Sign(header.ToByteArray()).ToArray();
        var transaction = new Transaction
        {
            Header = header,
            HeaderSignature = ByteString.CopyFrom(headerSignature),
            Payload = transferredEvent.ToByteString()
        };
        var request = new SendTransactionsRequest();
        request.Transactions.Add(transaction);

        await client.SendTransactionsAsync(request);
        var statusRequest = new GetTransactionStatusRequest
        {
            Id = Convert.ToBase64String(SHA256.HashData(transaction.ToByteArray()))
        };
        var began = DateTime.Now;
        while (true)
        {
            var status = await client.GetTransactionStatusAsync(statusRequest);

            if (status.Status == TransactionState.Committed)
                break;
            else if (status.Status == TransactionState.Failed)
                throw new Exception($"Failed to transfer certificate. Message: {status.Message}");
            else
                await Task.Delay(1000);

            if (DateTime.Now - began > TimeSpan.FromMinutes(1))
                throw new Exception("Timed out waiting for transaction to commit");
        }
    }
}
