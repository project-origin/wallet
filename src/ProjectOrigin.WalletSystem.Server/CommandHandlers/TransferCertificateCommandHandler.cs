using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using ProjectOrigin.WalletSystem.Server.Activities;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.CommandHandlers;

public record TransferCertificateCommand
{
    public required string Owner { get; init; }
    public required string Registry { get; init; }
    public required Guid CertificateId { get; init; }
    public required uint Quantity { get; init; }
    public required Guid Receiver { get; init; }
}

public class TransferCertificateCommandHandler : IConsumer<TransferCertificateCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransferCertificateCommandHandler> _logger;
    private readonly IEndpointNameFormatter _formatter;

    public TransferCertificateCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<TransferCertificateCommandHandler> logger,
        IEndpointNameFormatter formatter)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _formatter = formatter;
    }

    public async Task Consume(ConsumeContext<TransferCertificateCommand> context)
    {
        using var scope = _logger.BeginScope($"Consuming TransferCertificateCommand, Receiver Id: {context.Message.Receiver}");

        try
        {
            var msg = context.Message;

            var receiverEndpoint = await _unitOfWork.WalletRepository.GetReceiveEndpoint(msg.Receiver)
                ?? throw new InvalidOperationException($"The receiver deposit endpoint was not found for this transfer");

            IEnumerable<Slice> reservedSlices = await _unitOfWork.CertificateRepository.ReserveQuantity(msg.Owner, msg.Registry, msg.CertificateId, msg.Quantity);

            var remainderToTransfer = msg.Quantity;
            List<Task> tasks = new();
            foreach (var slice in reservedSlices)
            {
                var builder = new RoutingSlipBuilder(NewId.NextGuid());

                if (slice.Quantity <= remainderToTransfer)
                {
                    builder.AddActivity<TransferFullSliceActivity, TransferFullSliceArguments>(_formatter,
                        new()
                        {
                            SourceSliceId = slice.Id,
                            ReceiverDepositEndpointId = receiverEndpoint.Id
                        });
                    remainderToTransfer -= (uint)slice.Quantity;
                }
                else
                {
                    builder.AddActivity<TransferPartialSliceActivity, TransferPartialSliceArguments>(_formatter,
                        new()
                        {
                            SourceSliceId = slice.Id,
                            ReceiverDepositEndpointId = receiverEndpoint.Id,
                            Quantity = remainderToTransfer
                        });
                }

                var routingSlip = builder.Build();
                tasks.Add(context.Execute(routingSlip));
            }

            await Task.WhenAll(tasks);
            _unitOfWork.Commit();
            _logger.LogTrace("Transfer command complete.");
        }
        catch (InvalidOperationException ex)
        {
            _unitOfWork.Rollback();
            _logger.LogWarning(ex, "Transfer is not allowed.");
        }
        catch (Exception ex)
        {
            _unitOfWork.Rollback();
            _logger.LogError(ex, "failed to handle transfer");
        }
    }
}
