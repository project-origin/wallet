using Google.Protobuf;
using Grpc.Net.Client;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Exceptions;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Options;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProjectOrigin.Vault.EventHandlers;

public record TransferFullSliceRegistryTransactionArguments
{
    public required string RegistryName { get; set; }
    public required Guid CertificateId { get; set; }
    public required Guid SliceId { get; set; }
    public required Guid TransferredSliceId { get; set; }
    public required WalletAttribute[] WalletAttributes { get; set; }
    public required Guid ExternalEndpointId { get; set; }
    public RequestStatusArgs? RequestStatusArgs { get; set; }
}

public record TransferPartialSliceRegistryTransactionArguments
{
    public required string RegistryName { get; set; }
    public required Guid CertificateId { get; set; }
    public required Guid TransferredSliceId { get; set; }
    public required Guid RemainderSliceId { get; set; }
    public required Guid SourceSliceId { get; set; }
    public required uint Quantity { get; init; }
    public required WalletAttribute[] WalletAttributes { get; set; }
    public required Guid ExternalEndpointId { get; set; }
    public RequestStatusArgs? RequestStatusArgs { get; set; }
}

public class VaultSendRegistryTransactionConsumer : IConsumer<TransferFullSliceRegistryTransactionArguments>,
    IConsumer<TransferPartialSliceRegistryTransactionArguments>
{
    private readonly IOptions<NetworkOptions> _networkOptions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VaultSendRegistryTransactionConsumer> _logger;

    public VaultSendRegistryTransactionConsumer(IOptions<NetworkOptions> networkOptions, IUnitOfWork unitOfWork, ILogger<VaultSendRegistryTransactionConsumer> logger)
    {
        _networkOptions = networkOptions;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TransferFullSliceRegistryTransactionArguments> context)
    {
        _logger.LogInformation("Starting consumer: {Consumer} with arguments {Args}", nameof(VaultSendRegistryTransactionConsumer), nameof(TransferFullSliceRegistryTransactionArguments));

        var msg = context.Message;

        try
        {
            var externalEndpoint = await _unitOfWork.WalletRepository.GetExternalEndpoint(msg.ExternalEndpointId);
            var nextReceiverPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(externalEndpoint.Id);
            var receiverPublicKey = externalEndpoint.PublicKey.Derive(nextReceiverPosition).GetPublicKey();

            var sourceSlice = await _unitOfWork.CertificateRepository.GetWalletSlice(msg.SliceId);
            var transferredEvent = CreateTransferEvent(sourceSlice, receiverPublicKey);

            var sourceSlicePrivateKey = await _unitOfWork.WalletRepository.GetPrivateKeyForSlice(sourceSlice.Id);
            var transaction = sourceSlicePrivateKey.SignRegistryTransaction(transferredEvent.CertificateId, transferredEvent);

            await SendTransactionToRegistry(transaction);

            var full = new TransferFullSliceWaitCommittedTransactionArguments
            {
                CertificateId = msg.CertificateId,
                RegistryName = msg.RegistryName,
                SliceId = msg.SliceId,
                TransferredSliceId = msg.TransferredSliceId,
                TransactionId = transaction.ToShaId(),
                ExternalEndpointId = msg.ExternalEndpointId,
                RequestStatusArgs = msg.RequestStatusArgs,
                WalletAttributes = msg.WalletAttributes
            };
            await _unitOfWork.OutboxMessageRepository.Create(new OutboxMessage
            {
                Created = DateTimeOffset.UtcNow.ToUtcTime(),
                Id = Guid.NewGuid(),
                MessageType = typeof(TransferFullSliceWaitCommittedTransactionArguments).ToString(),
                JsonPayload = JsonSerializer.Serialize(full)
            });
            _unitOfWork.Commit();

            _logger.LogInformation("Ending consumer: {Consumer} with arguments {Args}", nameof(VaultSendRegistryTransactionConsumer), nameof(TransferFullSliceRegistryTransactionArguments));
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "Failed to communicate with the database.");
            throw new TransientException("Failed to communicate with the database.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending transactions to registry");
            throw;
        }
    }

    public async Task Consume(ConsumeContext<TransferPartialSliceRegistryTransactionArguments> context)
    {
        _logger.LogInformation("Starting consumer: {Consumer} with arguments {Args}", nameof(VaultSendRegistryTransactionConsumer), nameof(TransferPartialSliceRegistryTransactionArguments));

        var msg = context.Message;

        try
        {
            var sourceSlice = await _unitOfWork.CertificateRepository.GetWalletSlice(msg.SourceSliceId);
            var sourceEndpoint = await _unitOfWork.WalletRepository.GetWalletEndpoint(sourceSlice.WalletEndpointId);

            var remainder = (uint)sourceSlice.Quantity - msg.Quantity;

            var receiverEndpoints = await _unitOfWork.WalletRepository.GetExternalEndpoint(msg.ExternalEndpointId);
            var receiverPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(receiverEndpoints.Id);
            var receiverPublicKey = receiverEndpoints.PublicKey.Derive(receiverPosition).GetPublicKey();
            var receiverCommitment = new SecretCommitmentInfo(msg.Quantity);

            var remainderEndpoint = await _unitOfWork.WalletRepository.GetWalletRemainderEndpoint(sourceEndpoint.WalletId);
            var remainderPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(remainderEndpoint.Id);
            var remainderPublicKey = remainderEndpoint.PublicKey.Derive(remainderPosition).GetPublicKey();
            var remainderCommitment = new SecretCommitmentInfo(remainder);

            var slicedEvent = CreateSliceEvent(sourceSlice, new NewSlice(receiverCommitment, receiverPublicKey), new NewSlice(remainderCommitment, remainderPublicKey));
            var sourceSlicePrivateKey = await _unitOfWork.WalletRepository.GetPrivateKeyForSlice(sourceSlice.Id);
            var transaction = sourceSlicePrivateKey.SignRegistryTransaction(slicedEvent.CertificateId, slicedEvent);
            await SendTransactionToRegistry(transaction);

            var partial = new TransferPartialSliceWaitCommittedTransactionArguments
            {
                CertificateId = msg.CertificateId,
                RegistryName = msg.RegistryName,
                SourceSliceId = msg.SourceSliceId,
                TransferredSliceId = msg.TransferredSliceId,
                RemainderSliceId = msg.RemainderSliceId,
                TransactionId = transaction.ToShaId(),
                ExternalEndpointId = msg.ExternalEndpointId,
                RequestStatusArgs = msg.RequestStatusArgs,
                WalletAttributes = msg.WalletAttributes
            };
            await _unitOfWork.OutboxMessageRepository.Create(new OutboxMessage
            {
                Created = DateTimeOffset.UtcNow.ToUtcTime(),
                Id = Guid.NewGuid(),
                MessageType = typeof(TransferPartialSliceWaitCommittedTransactionArguments).ToString(),
                JsonPayload = JsonSerializer.Serialize(partial)
            });
            _unitOfWork.Commit();

            _logger.LogInformation("Ending consumer: {Consumer} with arguments {Args}", nameof(VaultSendRegistryTransactionConsumer), nameof(TransferPartialSliceRegistryTransactionArguments));
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "Failed to communicate with the database.");
            throw new TransientException("Failed to communicate with the database.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending transactions to registry");
            throw;
        }
    }

    private async Task SendTransactionToRegistry(Transaction transaction)
    {
        var registryName = transaction.Header.FederatedStreamId.Registry;

        var request = new SendTransactionsRequest();
        request.Transactions.Add(transaction);

        if (!_networkOptions.Value.Registries.TryGetValue(registryName, out var registryInfo))
            throw new ArgumentException($"Registry with name {registryName} not found in configuration.");

        using var channel = GrpcChannel.ForAddress(registryInfo.Url);

        var client = new RegistryService.RegistryServiceClient(channel);
        await client.SendTransactionsAsync(request);
    }

    private static TransferredEvent CreateTransferEvent(WalletSlice sourceSlice, IPublicKey receiverPublicKey)
    {
        var sliceCommitment = new PedersenCommitment.SecretCommitmentInfo((uint)sourceSlice.Quantity, sourceSlice.RandomR);

        var transferredEvent = new TransferredEvent
        {
            CertificateId = sourceSlice.GetFederatedStreamId(),
            NewOwner = new PublicKey
            {
                Content = ByteString.CopyFrom(receiverPublicKey.Export()),
                Type = KeyType.Secp256K1
            },
            SourceSliceHash = ByteString.CopyFrom(SHA256.HashData(sliceCommitment.Commitment.C))
        };
        return transferredEvent;
    }
    private sealed record NewSlice(SecretCommitmentInfo ci, IPublicKey Key);

    private static SlicedEvent CreateSliceEvent(WalletSlice sourceSlice, params NewSlice[] newSlices)
    {
        if (newSlices.Sum(s => s.ci.Message) != sourceSlice.Quantity)
            throw new InvalidOperationException();

        var certificateId = sourceSlice.GetFederatedStreamId();

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
