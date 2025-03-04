using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using Npgsql;
using ProjectOrigin.Vault.Activities;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Exceptions;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Models;

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
    private readonly IEndpointNameFormatter _formatter;
    private readonly ITransferMetrics _transferMetrics;

    public TransferCertificateCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<TransferCertificateCommandHandler> logger,
        IEndpointNameFormatter formatter,
        ITransferMetrics transferMetrics)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _formatter = formatter;
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
            List<Task> tasks = new();
            foreach (var slice in reservedSlices)
            {
                var builder = new RoutingSlipBuilder(msg.TransferRequestId);

                if (slice.Quantity <= remainderToTransfer)
                {
                    builder.AddActivity<TransferFullSliceActivity, TransferFullSliceArguments>(_formatter,
                        new()
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
                        });
                    remainderToTransfer -= (uint)slice.Quantity;
                }
                else
                {
                    builder.AddActivity<TransferPartialSliceActivity, TransferPartialSliceArguments>(_formatter,
                        new()
                        {
                            SourceSliceId = slice.Id,
                            ExternalEndpointId = receiverEndpoint.Id,
                            Quantity = remainderToTransfer,
                            HashedAttributes = msg.HashedAttributes,
                            RequestStatusArgs = new RequestStatusArgs
                            {
                                RequestId = msg.TransferRequestId,
                                Owner = msg.Owner,
                                RequestStatusType = RequestStatusType.Transfer
                            }
                        });
                }

                var routingSlip = builder.Build();
                await _unitOfWork.OutboxMessageRepository.Create(new OutboxMessage
                {
                    Created = DateTimeOffset.UtcNow.ToUtcTime(),
                    Id = Guid.NewGuid(),
                    MessageType = typeof(TransferCertificateCommand).ToString(),
                    JsonPayload = JsonSerializer.Serialize(context.Message)
                });
                tasks.Add(context.Execute(routingSlip));
            }

            await Task.WhenAll(tasks);
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
