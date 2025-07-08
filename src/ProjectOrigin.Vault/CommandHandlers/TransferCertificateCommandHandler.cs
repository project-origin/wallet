using MassTransit;
using Microsoft.Extensions.Logging;
using Npgsql;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.EventHandlers;
using ProjectOrigin.Vault.Exceptions;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProjectOrigin.Vault.CommandHandlers;

public record TransferCertificateCommand
{
    public required Guid TransferRequestId { get; init; }
    public required string Owner { get; init; }
    public required string Registry { get; init; }
    public required Guid CertificateId { get; init; }
    public required uint Quantity { get; init; }
    public required Guid Receiver { get; init; }
    public required string[] HashedAttributes { get; init; }
}

public class TransferCertificateCommandHandler : IConsumer<TransferCertificateCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransferCertificateCommandHandler> _logger;
    private readonly ITransferMetrics _transferMetrics;

    public TransferCertificateCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<TransferCertificateCommandHandler> logger,
        ITransferMetrics transferMetrics)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _transferMetrics = transferMetrics;
    }

    public async Task Consume(ConsumeContext<TransferCertificateCommand> context)
    {
        using var scope = _logger.BeginScope($"Consuming TransferCertificateCommand, Receiver Id: {context.Message.Receiver}");

        try
        {
            var msg = context.Message;

            var receiverEndpoint = await _unitOfWork.WalletRepository.GetExternalEndpoint(msg.Receiver)
                ?? throw new InvalidOperationException($"The external endpoint was not found for this transfer");

            IEnumerable<WalletSlice> reservedSlices = await _unitOfWork.CertificateRepository.ReserveQuantity(msg.Owner, msg.Registry, msg.CertificateId, msg.Quantity);

            var remainderToTransfer = msg.Quantity;
            foreach (var slice in reservedSlices)
            {
                if (slice.Quantity <= remainderToTransfer)
                {
                    var full = new TransferFullSliceArguments
                    {
                        SourceSliceId = slice.Id,
                        ExternalEndpointId = receiverEndpoint.Id,
                        HashedAttributes = msg.HashedAttributes,
                        RequestStatusArgs = new RequestStatusArgs
                        {
                            RequestId = msg.TransferRequestId,
                            Owner = msg.Owner,
                            RequestStatusType = RequestStatusType.Transfer
                        }
                    };
                    await _unitOfWork.OutboxMessageRepository.Create(new OutboxMessage
                    {
                        Created = DateTimeOffset.UtcNow.ToUtcTime(),
                        Id = Guid.NewGuid(),
                        MessageType = typeof(TransferFullSliceArguments).ToString(),
                        JsonPayload = JsonSerializer.Serialize(full)
                    });

                    remainderToTransfer -= (uint)slice.Quantity;
                }
                else
                {
                    var partial = new TransferPartialSliceArguments
                    {
                        ExternalEndpointId = receiverEndpoint.Id,
                        HashedAttributes = msg.HashedAttributes,
                        RequestStatusArgs = new RequestStatusArgs
                        {
                            RequestId = msg.TransferRequestId,
                            Owner = msg.Owner,
                            RequestStatusType = RequestStatusType.Transfer
                        },
                        Quantity = remainderToTransfer,
                        SourceSliceId = slice.Id
                    };
                    await _unitOfWork.OutboxMessageRepository.Create(new OutboxMessage
                    {
                        Created = DateTimeOffset.UtcNow.ToUtcTime(),
                        Id = Guid.NewGuid(),
                        MessageType = typeof(TransferPartialSliceArguments).ToString(),
                        JsonPayload = JsonSerializer.Serialize(partial)
                    });
                }
            }

            _unitOfWork.Commit();

            _logger.LogDebug("Transfer command complete.");
        }
        catch (InvalidOperationException ex)
        {
            _unitOfWork.Rollback();
            _logger.LogWarning(ex, "Transfer is not allowed.");
            await _unitOfWork.RequestStatusRepository.SetRequestStatus(context.Message.TransferRequestId, context.Message.Owner, RequestStatusState.Failed, failedReason: "Transfer is not allowed.");
            _unitOfWork.Commit();
            _transferMetrics.IncrementFailedTransfers();
        }
        catch (QuantityNotYetAvailableToReserveException ex)
        {
            _unitOfWork.Rollback();
            _logger.LogWarning(ex, "Failed to handle transfer at this time.");
            throw;
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "Failed to communicate with the database.");
            throw new TransientException("Failed to communicate with the database.", ex);
        }
        catch (Exception ex)
        {
            _unitOfWork.Rollback();
            _logger.LogError(ex, "Failed to handle transfer.");
            await _unitOfWork.RequestStatusRepository.SetRequestStatus(context.Message.TransferRequestId, context.Message.Owner, RequestStatusState.Failed, failedReason: "Failed to handle transfer.");
            _unitOfWork.Commit();
            _transferMetrics.IncrementFailedTransfers();
        }
    }
}
