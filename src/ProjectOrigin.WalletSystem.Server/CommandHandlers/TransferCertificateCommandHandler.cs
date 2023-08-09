using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Net.Client;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Common.V1;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Options;

namespace ProjectOrigin.WalletSystem.Server.CommandHandlers;

public record TransferCertificateCommand(string Owner, string Registry, Guid CertificateId, uint Quantity, Guid Receiver);

public class TransferCertificateCommandHandler : IConsumer<TransferCertificateCommand>
{
    private readonly UnitOfWork _unitOfWork;
    private readonly ILogger<TransferCertificateCommandHandler> _logger;
    private readonly IOptions<RegistryOptions> _registryOptions;
    private readonly IOptions<ServiceOptions> _walletSystemOptions;
    private TimeSpan timeout = TimeSpan.FromMinutes(1);


    public TransferCertificateCommandHandler(UnitOfWork unitOfWork, ILogger<TransferCertificateCommandHandler> logger, IOptions<RegistryOptions> registryOptions, IOptions<ServiceOptions> walletSystemOptions)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _registryOptions = registryOptions;
        _walletSystemOptions = walletSystemOptions;
    }

    public async Task Consume(ConsumeContext<TransferCertificateCommand> context)
    {
        using var scope = _logger.BeginScope("Consuming TransferCertificateCommand, Receiver Id: {msg.Receiver}");
        try
        {
            var msg = context.Message;

            var receiverDepositEndpoint = await _unitOfWork.WalletRepository.GetDepositEndpoint(msg.Receiver)
                ?? throw new InvalidOperationException($"The receiver deposit endpoint was not found for this transfer");

            var availableSlices = await _unitOfWork.CertificateRepository.GetOwnerAvailableSlices(msg.Registry, msg.CertificateId, msg.Owner);
            if (availableSlices.IsEmpty())
                throw new InvalidOperationException($"Owner has no available slices to transfer");

            if (availableSlices.Sum(slice => slice.Quantity) < msg.Quantity)
                throw new InvalidOperationException($"Owner has less to transfer than available");

            IEnumerable<Slice> reservedSlices = await ReserveRequiredSlices(availableSlices, msg.Quantity);

            var remainderToTransfer = msg.Quantity;
            foreach (var slice in reservedSlices)
            {
                _logger.LogTrace($"Preparing to transfer");

                Slice newSlice;
                if (slice.Quantity <= remainderToTransfer)
                {
                    remainderToTransfer -= (uint)slice.Quantity;
                    newSlice = await Transfer(slice, receiverDepositEndpoint);
                }
                else
                {
                    newSlice = await Transfer(slice, receiverDepositEndpoint, remainderToTransfer);
                }

                await SendInformationToReceiverWallet(msg.Registry, newSlice, receiverDepositEndpoint);
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Transfer is not allowed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "failed to handle transfer");
        }
    }

    private async Task SendInformationToReceiverWallet(string registryName, Slice newSlice, DepositEndpoint receiverDepositEndpoint)
    {
        if (_walletSystemOptions.Value.EndpointAddress == receiverDepositEndpoint.Endpoint)
        {
            _logger.LogTrace("Receiver is local.");

            var receiverEndpoint = await _unitOfWork.WalletRepository.GetDepositEndpointFromPublicKey(receiverDepositEndpoint.PublicKey)
                ?? throw new Exception("Local receiver wallet could not be found");

            var slice = new Slice(Guid.NewGuid(),
                                  receiverEndpoint.Id,
                                  newSlice.DepositEndpointPosition,
                                  newSlice.RegistryId,
                                  newSlice.CertificateId,
                                  newSlice.Quantity,
                                  newSlice.RandomR,
                                  SliceState.Available);

            await _unitOfWork.CertificateRepository.InsertSlice(slice);
            _unitOfWork.Commit();

            _logger.LogTrace("Slice inserted locally into receiver wallet.");
        }
        else
        {
            try
            {
                _logger.LogTrace("Preparing to send information to receiver");

                var request = new V1.ReceiveRequest
                {
                    WalletDepositEndpointPublicKey = ByteString.CopyFrom(receiverDepositEndpoint.PublicKey.Export()),
                    WalletDepositEndpointPosition = (uint)newSlice.DepositEndpointPosition,
                    CertificateId = new FederatedStreamId
                    {
                        Registry = registryName,
                        StreamId = new Uuid { Value = newSlice.CertificateId.ToString() }
                    },
                    Quantity = (uint)newSlice.Quantity,
                    RandomR = ByteString.CopyFrom(newSlice.RandomR)
                };

                using var channel = GrpcChannel.ForAddress(receiverDepositEndpoint.Endpoint);
                var client = new V1.ReceiveSliceService.ReceiveSliceServiceClient(channel);

                _logger.LogTrace("Sending information to receiver");
                await client.ReceiveSliceAsync(request);

                _logger.LogTrace("Information Sent to receiver");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send information to receiver");
                throw;
            }
        }
    }

    private async Task<IEnumerable<Slice>> ReserveRequiredSlices(IEnumerable<Slice> slices, uint quantity)
    {
        _logger.LogTrace($"Reserving slices to transfer.");

        var sumSlicesTaken = 0L;
        var takenSlices = slices
            .OrderBy(slice => slice.Quantity)
            .TakeWhile(slice => { var needsMore = sumSlicesTaken < quantity; sumSlicesTaken += slice.Quantity; return needsMore; })
            .ToList();

        foreach (var slice in takenSlices)
        {
            await _unitOfWork.CertificateRepository.SetSliceState(slice, SliceState.Slicing);
        }
        _unitOfWork.Commit();

        _logger.LogTrace($"{takenSlices.Count} slices reserved.");

        return takenSlices;
    }

    internal async Task<Slice> Transfer(Slice sourceSlice, DepositEndpoint receiverDepositEndpoint)
    {
        var nextReceiverPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(receiverDepositEndpoint.Id);
        var receiverPublicKey = receiverDepositEndpoint.PublicKey.Derive(nextReceiverPosition).GetPublicKey();

        var transferredSlice = new Slice(Guid.NewGuid(), receiverDepositEndpoint.Id, nextReceiverPosition, sourceSlice.RegistryId, sourceSlice.CertificateId, sourceSlice.Quantity, sourceSlice.RandomR, SliceState.Registering);
        await _unitOfWork.CertificateRepository.InsertSlice(transferredSlice);
        _unitOfWork.Commit();

        var registry = await _unitOfWork.RegistryRepository.GetRegistryFromId(sourceSlice.RegistryId);

        var transferredEvent = CreateTransferEvent(registry.Name, sourceSlice, receiverPublicKey);

        var sourceSlicePrivateKey = await GetPrivateKey(sourceSlice);
        var transaction = CreateAndSignTransaction(transferredEvent.CertificateId, transferredEvent, sourceSlicePrivateKey);
        await SendTransactions(transaction);
        await WaitForCommitted(transaction);

        await _unitOfWork.CertificateRepository.SetSliceState(sourceSlice, SliceState.Sliced);
        await _unitOfWork.CertificateRepository.SetSliceState(transferredSlice, SliceState.Transferred);
        _unitOfWork.Commit();

        return transferredSlice;
    }

    internal async Task<Slice> Transfer(Slice sourceSlice, DepositEndpoint receiverDepositEndpoint, uint quantity)
    {
        var nextReceiverPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(receiverDepositEndpoint.Id);
        var receiverPublicKey = receiverDepositEndpoint.PublicKey.Derive(nextReceiverPosition).GetPublicKey();

        var sourceDepositEndpoint = await _unitOfWork.WalletRepository.GetDepositEndpoint(sourceSlice.DepositEndpointId);

        DepositEndpoint remainderDepositEndpoint = await _unitOfWork.WalletRepository.GetWalletRemainderDepositEndpoint(sourceDepositEndpoint.WalletId ?? throw new Exception());
        var nextRemainderPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(remainderDepositEndpoint.Id); ;
        var remainderPublicKey = remainderDepositEndpoint.PublicKey.Derive(nextReceiverPosition).GetPublicKey();

        var remainder = (uint)sourceSlice.Quantity - quantity;

        var commitmentQuantity = new SecretCommitmentInfo(quantity);
        var commitmentRemainder = new SecretCommitmentInfo(remainder);

        var transferredSlice = new Slice(Guid.NewGuid(), receiverDepositEndpoint.Id, nextReceiverPosition, sourceSlice.RegistryId, sourceSlice.CertificateId, commitmentQuantity.Message, commitmentQuantity.BlindingValue.ToArray(), SliceState.Registering);
        await _unitOfWork.CertificateRepository.InsertSlice(transferredSlice);
        var remainderSlice = new Slice(Guid.NewGuid(), remainderDepositEndpoint.Id, nextRemainderPosition, sourceSlice.RegistryId, sourceSlice.CertificateId, commitmentRemainder.Message, commitmentRemainder.BlindingValue.ToArray(), SliceState.Registering);
        await _unitOfWork.CertificateRepository.InsertSlice(remainderSlice);
        _unitOfWork.Commit();

        var registry = await _unitOfWork.RegistryRepository.GetRegistryFromId(sourceSlice.RegistryId);
        var slicedEvent = CreateSliceEvent(registry.Name, sourceSlice, new NewSlice(commitmentQuantity, receiverPublicKey), new NewSlice(commitmentRemainder, remainderPublicKey));

        var sourceSlicePrivateKey = await GetPrivateKey(sourceSlice);
        Transaction transaction = CreateAndSignTransaction(slicedEvent.CertificateId, slicedEvent, sourceSlicePrivateKey);

        await SendTransactions(transaction);
        await WaitForCommitted(transaction);

        await _unitOfWork.CertificateRepository.SetSliceState(sourceSlice, SliceState.Sliced);
        await _unitOfWork.CertificateRepository.SetSliceState(transferredSlice, SliceState.Transferred);
        await _unitOfWork.CertificateRepository.SetSliceState(remainderSlice, SliceState.Available);
        _unitOfWork.Commit();

        return transferredSlice;
    }

    private static Transaction CreateAndSignTransaction(FederatedStreamId certificateId, IMessage @event, IHDPrivateKey slicePrivateKey)
    {
        var header = new TransactionHeader
        {
            FederatedStreamId = certificateId,
            PayloadType = @event.Descriptor.FullName,
            PayloadSha512 = ByteString.CopyFrom(SHA512.HashData(@event.ToByteArray())),
            Nonce = Guid.NewGuid().ToString(),
        };

        var transaction = new Transaction
        {
            Header = header,
            HeaderSignature = ByteString.CopyFrom(slicePrivateKey.Sign(header.ToByteArray())),
            Payload = @event.ToByteString()
        };

        return transaction;
    }

    private async Task SendTransactions(params Transaction[] transactions)
    {
        var registryName = transactions.First().Header.FederatedStreamId.Registry;
        if (!transactions.All(x => x.Header.FederatedStreamId.Registry.Equals(registryName)))
            throw new NotSupportedException("Only transactions for a single registry is supported");

        var request = new SendTransactionsRequest();
        request.Transactions.AddRange(transactions);

        var registryUrl = _registryOptions.Value.RegistryUrls[registryName];
        using var channel = GrpcChannel.ForAddress(registryUrl);

        var client = new RegistryService.RegistryServiceClient(channel);
        await client.SendTransactionsAsync(request);
    }

    private async Task WaitForCommitted(Transaction transaction)
    {
        var statusRequest = new GetTransactionStatusRequest
        {
            Id = Convert.ToBase64String(SHA256.HashData(transaction.ToByteArray()))
        };

        var began = DateTime.Now;
        while (true)
        {
            var registryUrl = _registryOptions.Value.RegistryUrls[transaction.Header.FederatedStreamId.Registry];
            using var channel = GrpcChannel.ForAddress(registryUrl);

            var client = new RegistryService.RegistryServiceClient(channel);
            var status = await client.GetTransactionStatusAsync(statusRequest);

            if (status.Status == TransactionState.Committed)
                break;
            else if (status.Status == TransactionState.Failed)
                throw new Exception($"Failed to transfer certificate. Message: {status.Message}");
            else
                await Task.Delay(1000);

            if (DateTime.Now - began > timeout)
                throw new Exception("Timed out waiting for transaction to commit");
        }
    }

    private async Task<IHDPrivateKey> GetPrivateKey(Slice slice)
    {
        var sourceEndpoint = await _unitOfWork.WalletRepository.GetDepositEndpoint(slice.DepositEndpointId);

        if (sourceEndpoint.WalletId is null || sourceEndpoint.WalletPosition is null)
            throw new InvalidOperationException("Slice is not in wallet, transactions not posible.");

        var wallet = await _unitOfWork.WalletRepository.GetWallet(sourceEndpoint.WalletId.Value);

        var slicePrivateKey = wallet.PrivateKey
            .Derive(sourceEndpoint.WalletPosition.Value)
            .Derive(slice.DepositEndpointPosition);

        return slicePrivateKey;
    }

    private TransferredEvent CreateTransferEvent(string registryName, Slice sourceSlice, IPublicKey receiverPublicKey)
    {
        var sliceCommitment = new PedersenCommitment.SecretCommitmentInfo((uint)sourceSlice.Quantity, sourceSlice.RandomR);

        var certificateId = new ProjectOrigin.Common.V1.FederatedStreamId
        {
            Registry = registryName,
            StreamId = new ProjectOrigin.Common.V1.Uuid { Value = sourceSlice.CertificateId.ToString() }
        };

        var transferredEvent = new TransferredEvent
        {
            CertificateId = certificateId,
            NewOwner = new PublicKey
            {
                Content = ByteString.CopyFrom(receiverPublicKey.Export()),
                Type = KeyType.Secp256K1
            },
            SourceSliceHash = ByteString.CopyFrom(SHA256.HashData(sliceCommitment.Commitment.C))
        };
        return transferredEvent;
    }

    private record NewSlice(SecretCommitmentInfo ci, IPublicKey Key);

    private SlicedEvent CreateSliceEvent(string registryName, Slice sourceSlice, params NewSlice[] newSlices)
    {
        if (newSlices.Sum(s => s.ci.Message) != sourceSlice.Quantity)
            throw new InvalidOperationException();

        var certificateId = new ProjectOrigin.Common.V1.FederatedStreamId
        {
            Registry = registryName,
            StreamId = new ProjectOrigin.Common.V1.Uuid { Value = sourceSlice.CertificateId.ToString() }
        };

        var sourceSliceCommitment = new PedersenCommitment.SecretCommitmentInfo((uint)sourceSlice.Quantity, sourceSlice.RandomR);
        var sumOfNewSlices = newSlices.Select(newSlice => newSlice.ci).Aggregate((left, right) => left + right);
        var equalityProof = SecretCommitmentInfo.CreateEqualityProof(sourceSliceCommitment, sumOfNewSlices, certificateId.StreamId.Value);

        var slicedEvent = new SlicedEvent
        {
            CertificateId = certificateId,
            SumProof = ByteString.CopyFrom(equalityProof),
            SourceSliceHash = ByteString.CopyFrom(SHA256.HashData(sourceSliceCommitment.Commitment.C))
        };

        foreach (var newSlice in newSlices)
        {
            var poSlice = new SlicedEvent.Types.Slice
            {
                NewOwner = new PublicKey
                {
                    Type = KeyType.Secp256K1,
                    Content = ByteString.CopyFrom(newSlice.Key.Export())
                },
                Quantity = new ProjectOrigin.Electricity.V1.Commitment
                {
                    Content = ByteString.CopyFrom(newSlice.ci.Commitment.C),
                    RangeProof = ByteString.CopyFrom(newSlice.ci.CreateRangeProof(certificateId.StreamId.Value))
                }
            };
            slicedEvent.NewSlices.Add(poSlice);
        }

        return slicedEvent;
    }
}
