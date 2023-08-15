using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.WalletSystem.Server.Activities;
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
    private readonly IEndpointNameFormatter _formatter;
    private TimeSpan timeout = TimeSpan.FromMinutes(1);

    public TransferCertificateCommandHandler(
        UnitOfWork unitOfWork,
        ILogger<TransferCertificateCommandHandler> logger,
        IOptions<RegistryOptions> registryOptions,
        IOptions<ServiceOptions> walletSystemOptions,
        IEndpointNameFormatter formatter)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _registryOptions = registryOptions;
        _walletSystemOptions = walletSystemOptions;
        _formatter = formatter;
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
            List<Task> tasks = new();
            foreach (var slice in reservedSlices)
            {
                var builder = new RoutingSlipBuilder(NewId.NextGuid());

                if (slice.Quantity <= remainderToTransfer)
                {
                    builder.AddActivity<TransferFullSliceActivity, TransferFullSliceArguments>(_formatter,
                        new(slice.Id, receiverDepositEndpoint.Id));
                }
                else
                {
                    builder.AddActivity<TransferPartialSliceActivity, TransferPartialWholeSliceArguments>(_formatter,
                        new(slice.Id, receiverDepositEndpoint.Id, remainderToTransfer));
                }

                var routingSlip = builder.Build();

                tasks.Add(context.Execute(routingSlip));
            }

            await Task.WhenAll(tasks);
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
            await _unitOfWork.CertificateRepository.SetSliceState(slice.Id, SliceState.Slicing);
        }
        _unitOfWork.Commit();

        _logger.LogTrace($"{takenSlices.Count} slices reserved.");

        return takenSlices;
    }
}
