using Google.Protobuf;
using MassTransit;
using Microsoft.Extensions.Logging;
using Npgsql;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Exceptions;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Models;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace ProjectOrigin.Vault.EventHandlers;

public record TransferPartialSliceArguments
{
    public required Guid SourceSliceId { get; init; }
    public required Guid ExternalEndpointId { get; init; }
    public required uint Quantity { get; init; }
    public required string[] HashedAttributes { get; init; }
    public required RequestStatusArgs RequestStatusArgs { get; init; }
}

public class VaultTransferPartialSliceConsumer : IConsumer<TransferPartialSliceArguments>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VaultTransferPartialSliceConsumer> _logger;
    private readonly ITransferMetrics _transferMetrics;

    public VaultTransferPartialSliceConsumer(
        IUnitOfWork unitOfWork,
        ILogger<VaultTransferPartialSliceConsumer> logger,
        ITransferMetrics transferMetrics)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _transferMetrics = transferMetrics;
    }

    public async Task Consume(ConsumeContext<TransferPartialSliceArguments> context)
    {
        var msg = context.Message;

        _logger.LogInformation("Starting consumer: {Consumer}, RequestId: {RequestId} ", nameof(VaultTransferPartialSliceConsumer), msg.RequestStatusArgs.RequestId);

        try
        {
            var sourceSlice = await _unitOfWork.CertificateRepository.GetWalletSlice(msg.SourceSliceId);
            var sourceEndpoint = await _unitOfWork.WalletRepository.GetWalletEndpoint(sourceSlice.WalletEndpointId);

            var quantity = msg.Quantity;
            var remainder = (uint)sourceSlice.Quantity - quantity;

            var receiverEndpoints = await _unitOfWork.WalletRepository.GetExternalEndpoint(msg.ExternalEndpointId);
            var receiverPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(receiverEndpoints.Id);
            var receiverPublicKey = receiverEndpoints.PublicKey.Derive(receiverPosition).GetPublicKey();
            var receiverCommitment = new SecretCommitmentInfo(quantity);
            var transferredSlice = new TransferredSlice
            {
                Id = Guid.NewGuid(),
                ExternalEndpointId = receiverEndpoints.Id,
                ExternalEndpointPosition = receiverPosition,
                RegistryName = sourceSlice.RegistryName,
                CertificateId = sourceSlice.CertificateId,
                Quantity = receiverCommitment.Message,
                RandomR = receiverCommitment.BlindingValue.ToArray(),
                State = TransferredSliceState.Registering
            };
            await _unitOfWork.TransferRepository.InsertTransferredSlice(transferredSlice);

            var remainderEndpoint = await _unitOfWork.WalletRepository.GetWalletRemainderEndpoint(sourceEndpoint.WalletId);
            var remainderPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(remainderEndpoint.Id);
            var remainderPublicKey = remainderEndpoint.PublicKey.Derive(remainderPosition).GetPublicKey();
            var remainderCommitment = new SecretCommitmentInfo(remainder);
            var remainderSlice = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = remainderEndpoint.Id,
                WalletEndpointPosition = remainderPosition,
                RegistryName = sourceSlice.RegistryName,
                CertificateId = sourceSlice.CertificateId,
                Quantity = remainderCommitment.Message,
                RandomR = remainderCommitment.BlindingValue.ToArray(),
                State = WalletSliceState.Registering
            };
            await _unitOfWork.CertificateRepository.InsertWalletSlice(remainderSlice);

            var slicedEvent = CreateSliceEvent(sourceSlice, new NewSlice(receiverCommitment, receiverPublicKey), new NewSlice(remainderCommitment, remainderPublicKey));
            var sourceSlicePrivateKey = await _unitOfWork.WalletRepository.GetPrivateKeyForSlice(sourceSlice.Id);
            var transaction = sourceSlicePrivateKey.SignRegistryTransaction(slicedEvent.CertificateId, slicedEvent);
            var walletAttributes = await _unitOfWork.CertificateRepository.GetWalletAttributes(sourceEndpoint.WalletId, sourceSlice.CertificateId, sourceSlice.RegistryName, msg.HashedAttributes);

            _unitOfWork.Commit();

            _logger.LogInformation("Ending consumer: {Consumer}, RequestId: {RequestId} ", nameof(VaultTransferPartialSliceConsumer), msg.RequestStatusArgs.RequestId);

            await context.Publish<TransferPartialSliceRegistryTransactionArguments>(new TransferPartialSliceRegistryTransactionArguments
            {
                Transaction = transaction,
                WalletAttributes = walletAttributes.ToArray(),
                ExternalEndpointId = receiverEndpoints.Id,
                TransferredSliceId = transferredSlice.Id,
                CertificateId = transferredSlice.CertificateId,
                RegistryName = transaction.Header.FederatedStreamId.Registry,
                RemainderSliceId = remainderSlice.Id,
                RequestStatusArgs = msg.RequestStatusArgs,
                SourceSliceId = sourceSlice.Id
            });
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "Failed to communicate with the database.");
            throw new TransientException("Failed to communicate with the database.", ex);
        }
        catch (Exception ex)
        {
            _unitOfWork.Rollback();
            _logger.LogError(ex, "Error sending partial slice transfer transactions to registry");
            await _unitOfWork.RequestStatusRepository.SetRequestStatus(msg.RequestStatusArgs.RequestId, msg.RequestStatusArgs.Owner, RequestStatusState.Failed, failedReason: "Error sending partial slice transfer transactions to registry.");
            _unitOfWork.Commit();
            _transferMetrics.IncrementFailedTransfers();
            throw;
        }
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
