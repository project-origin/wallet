using MassTransit;
using Microsoft.Extensions.Logging;
using Npgsql;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Exceptions;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Models;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProjectOrigin.Vault.EventHandlers;

public record TransferFullSliceArguments
{
    public required Guid SourceSliceId { get; init; }
    public required Guid ExternalEndpointId { get; init; }
    public required string[] HashedAttributes { get; init; }
    public required RequestStatusArgs RequestStatusArgs { get; init; }
}

public class VaultTransferFullSliceConsumer : IConsumer<TransferFullSliceArguments>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VaultTransferFullSliceConsumer> _logger;
    private readonly ITransferMetrics _transferMetrics;

    public VaultTransferFullSliceConsumer(
        IUnitOfWork unitOfWork,
        ILogger<VaultTransferFullSliceConsumer> logger,
        ITransferMetrics transferMetrics)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _transferMetrics = transferMetrics;
    }

    public async Task Consume(ConsumeContext<TransferFullSliceArguments> context)
    {
        var msg = context.Message;

        _logger.LogInformation("Starting consumer: {Consumer}, RequestId: {RequestId} ", nameof(VaultTransferFullSliceConsumer), msg.RequestStatusArgs.RequestId);

        try
        {
            var sourceSlice = await _unitOfWork.CertificateRepository.GetWalletSlice(msg.SourceSliceId);
            var sourceEndpoint = await _unitOfWork.WalletRepository.GetWalletEndpoint(sourceSlice.WalletEndpointId);
            var externalEndpoint = await _unitOfWork.WalletRepository.GetExternalEndpoint(msg.ExternalEndpointId);
            var nextReceiverPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(externalEndpoint.Id);

            var transferredSlice = new TransferredSlice
            {
                Id = Guid.NewGuid(),
                ExternalEndpointId = externalEndpoint.Id,
                ExternalEndpointPosition = nextReceiverPosition,
                RegistryName = sourceSlice.RegistryName,
                CertificateId = sourceSlice.CertificateId,
                Quantity = sourceSlice.Quantity,
                RandomR = sourceSlice.RandomR,
                State = TransferredSliceState.Registering
            };
            await _unitOfWork.TransferRepository.InsertTransferredSlice(transferredSlice);

            _logger.LogInformation($"Registering transfer for certificateId {sourceSlice.CertificateId}");

            var walletAttributes = await _unitOfWork.CertificateRepository.GetWalletAttributes(sourceEndpoint.WalletId, sourceSlice.CertificateId, sourceSlice.RegistryName, msg.HashedAttributes);

            _logger.LogInformation("Ending consumer: {Consumer}, RequestId: {RequestId} ", nameof(VaultTransferFullSliceConsumer), msg.RequestStatusArgs.RequestId);

            var message = new TransferFullSliceRegistryTransactionArguments
            {
                CertificateId = sourceSlice.CertificateId,
                RegistryName = sourceSlice.RegistryName,
                SliceId = sourceSlice.Id,
                TransferredSliceId = transferredSlice.Id,
                RequestStatusArgs = msg.RequestStatusArgs,
                ExternalEndpointId = externalEndpoint.Id,
                WalletAttributes = walletAttributes.ToArray()
            };
            await _unitOfWork.OutboxMessageRepository.Create(new OutboxMessage
            {
                Created = DateTimeOffset.UtcNow.ToUtcTime(),
                Id = Guid.NewGuid(),
                MessageType = typeof(TransferFullSliceRegistryTransactionArguments).ToString(),
                JsonPayload = JsonSerializer.Serialize(message)
            });

            _unitOfWork.Commit();
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "Failed to communicate with the database.");
            throw new TransientException("Failed to communicate with the database.", ex);
        }
        catch (Exception ex)
        {
            _unitOfWork.Rollback();
            _logger.LogError(ex, "Error sending full slice transfer transactions to registry");
            await _unitOfWork.RequestStatusRepository.SetRequestStatus(msg.RequestStatusArgs.RequestId, msg.RequestStatusArgs.Owner, RequestStatusState.Failed, failedReason: "Error sending full slice transfer transactions to registry.");
            _unitOfWork.Commit();
            _transferMetrics.IncrementFailedTransfers();
            throw;
        }
    }
}
