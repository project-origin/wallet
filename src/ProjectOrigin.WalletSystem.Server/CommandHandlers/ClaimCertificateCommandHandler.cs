using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Courier.Contracts;
using Microsoft.Extensions.Logging;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.CommandHandlers;

public record ClaimCertificateCommand
{
    public required string Owner { get; init; }
    public required string ConsumptionRegistry { get; init; }
    public required Guid ConsumptionCertificateId { get; init; }
    public required string ProductionRegistry { get; init; }
    public required Guid ProductionCertificateId { get; init; }
    public required uint Quantity { get; init; }
}

public class ClaimCertificateCommandHandler : IConsumer<ClaimCertificateCommand>
{
    private readonly ILogger<ClaimCertificateCommandHandler> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRegistryProcessBuilder _processBuilder;

    public ClaimCertificateCommandHandler(
        ILogger<ClaimCertificateCommandHandler> logger,
        IUnitOfWork unitOfWork,
        IRegistryProcessBuilder processBuilder)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _processBuilder = processBuilder;
    }

    public async Task Consume(ConsumeContext<ClaimCertificateCommand> context)
    {
        using var scope = _logger.BeginScope($"Consuming {nameof(ClaimCertificateCommand)} {context.CorrelationId}");

        try
        {
            var msg = context.Message;

            var reservedConsumptionSlices = await _unitOfWork.CertificateRepository.ReserveQuantity(msg.Owner, msg.ConsumptionRegistry, msg.ConsumptionCertificateId, msg.Quantity);
            var reservedProductionSlices = await _unitOfWork.CertificateRepository.ReserveQuantity(msg.Owner, msg.ProductionRegistry, msg.ProductionCertificateId, msg.Quantity);

            var routingSlip = await BuildClaimRoutingSlip(msg.Quantity, reservedConsumptionSlices, reservedProductionSlices);

            await context.Execute(routingSlip);
            _unitOfWork.Commit();

            _logger.LogTrace($"Claim command complete.");
        }
        catch (InvalidOperationException ex)
        {
            _unitOfWork.Rollback();
            _logger.LogWarning(ex, "Claim is not allowed.");
        }
        catch (Exception ex)
        {
            _unitOfWork.Rollback();
            _logger.LogError(ex, "failed to handle claim");
        }
    }

    /// <summary>
    /// This method builds a routing slip with a greedy algorithm.
    /// It slips slices as required to claim the requested quantity.
    /// </summary>
    /// <param name="reservedConsumptionSlices"></param>
    /// <param name="reservedProductionSlices"></param>
    private async Task<RoutingSlip> BuildClaimRoutingSlip(long quantity, IList<Slice> reservedConsumptionSlices, IList<Slice> reservedProductionSlices)
    {
        var remainderToClaim = quantity;
        Slice? productionRemainderSlice = null;

        foreach (var loopConsumptionSlice in reservedConsumptionSlices)
        {
            Slice? consumptionRemainderSlice = loopConsumptionSlice;
            while (consumptionRemainderSlice is not null)
            {
                if (productionRemainderSlice is null)
                {
                    productionRemainderSlice = reservedProductionSlices.PopFirst();
                }

                if (consumptionRemainderSlice.Quantity > remainderToClaim)
                {
                    var (quantitySlice, remainderSlice) = await _processBuilder.SplitSlice(consumptionRemainderSlice, remainderToClaim);
                    _processBuilder.SetSliceStates(new() { { remainderSlice.Id, SliceState.Available } });
                    consumptionRemainderSlice = quantitySlice;
                }

                if (productionRemainderSlice.Quantity < consumptionRemainderSlice.Quantity)
                {
                    var (quantitySlice, remainderSlice) = await _processBuilder.SplitSlice(consumptionRemainderSlice, productionRemainderSlice.Quantity);
                    await _processBuilder.Claim(productionRemainderSlice, quantitySlice);

                    remainderToClaim -= quantitySlice.Quantity;
                    productionRemainderSlice = null; // production slice is fully claimed
                    consumptionRemainderSlice = remainderSlice;
                }
                else if (productionRemainderSlice.Quantity > consumptionRemainderSlice.Quantity)
                {
                    var (quantitySlice, remainderSlice) = await _processBuilder.SplitSlice(productionRemainderSlice, consumptionRemainderSlice.Quantity);
                    await _processBuilder.Claim(quantitySlice, consumptionRemainderSlice);

                    remainderToClaim -= consumptionRemainderSlice.Quantity;
                    productionRemainderSlice = remainderSlice;
                    consumptionRemainderSlice = null; // consumption slice is fully claimed
                }
                else // productionRemainderSlice.Quantity == consumptionRemainderSlice.Quantity
                {
                    await _processBuilder.Claim(productionRemainderSlice, consumptionRemainderSlice);

                    remainderToClaim -= consumptionRemainderSlice.Quantity;
                    productionRemainderSlice = null; // production slice is fully claimed
                    consumptionRemainderSlice = null; // consumption slice is fully claimed
                }
            }
        }

        if (productionRemainderSlice is not null)
        {
            // if last production slice has remainder, it should be returned to the available
            _processBuilder.SetSliceStates(new() { { productionRemainderSlice.Id, SliceState.Available } });
        }

        return _processBuilder.Build();
    }
}
