using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Courier.Contracts;
using Microsoft.Extensions.Logging;
using Npgsql;
using ProjectOrigin.Vault.Activities;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Exceptions;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Models;

namespace ProjectOrigin.Vault.CommandHandlers;

public record ClaimCertificateCommand
{
    public required string Owner { get; init; }
    public required Guid ClaimId { get; init; }
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
    private readonly IRegistryProcessBuilderFactory _processBuilderFactory;
    private readonly IClaimMetrics _claimMetrics;

    public ClaimCertificateCommandHandler(
        ILogger<ClaimCertificateCommandHandler> logger,
        IUnitOfWork unitOfWork,
        IRegistryProcessBuilderFactory registryProcessBuilderFactory,
        IClaimMetrics claimMetrics)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _processBuilderFactory = registryProcessBuilderFactory;
        _claimMetrics = claimMetrics;
    }

    public async Task Consume(ConsumeContext<ClaimCertificateCommand> context)
    {
        using var scope = _logger.BeginScope($"Consuming {nameof(ClaimCertificateCommand)} {context.CorrelationId}");

        try
        {
            var msg = context.Message;

            var reservedConsumptionSlices = await _unitOfWork.CertificateRepository.ReserveQuantity(msg.Owner, msg.ConsumptionRegistry, msg.ConsumptionCertificateId, msg.Quantity);
            var reservedProductionSlices = await _unitOfWork.CertificateRepository.ReserveQuantity(msg.Owner, msg.ProductionRegistry, msg.ProductionCertificateId, msg.Quantity);

            var processBuilder = _processBuilderFactory.Create(msg.ClaimId, msg.Owner, _unitOfWork);

            var routingSlip = await BuildClaimRoutingSlip(processBuilder, msg.Quantity, reservedConsumptionSlices, reservedProductionSlices, new RequestStatusArgs { Owner = msg.Owner, RequestId = msg.ClaimId, RequestStatusType = RequestStatusType.Claim });

            await context.Execute(routingSlip);
            await _unitOfWork.OutboxMessageRepository.Create(new OutboxMessage
            {
                Created = DateTimeOffset.UtcNow.ToUtcTime(),
                Id = Guid.NewGuid(),
                MessageType = typeof(ClaimCertificateCommand).ToString(),
                JsonPayload = JsonSerializer.Serialize(msg)
            });

            _unitOfWork.Commit();
            _logger.LogDebug($"Claim command complete.");
        }
        catch (InvalidOperationException ex)
        {
            _unitOfWork.Rollback();
            _logger.LogWarning(ex, "Claim is not allowed.");
            await _unitOfWork.RequestStatusRepository.SetRequestStatus(context.Message.ClaimId, context.Message.Owner, RequestStatusState.Failed, failedReason: "Claim is not allowed.");
            _unitOfWork.Commit();
            _claimMetrics.IncrementFailedClaims();
        }
        catch (QuantityNotYetAvailableToReserveException ex)
        {
            _unitOfWork.Rollback();
            // Add jitter to delay the retry
            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(5, 10)));
            _logger.LogWarning(ex, "Failed to handle claim at this time.");
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
            _logger.LogError(ex, "failed to handle claim");
            await _unitOfWork.RequestStatusRepository.SetRequestStatus(context.Message.ClaimId, context.Message.Owner, RequestStatusState.Failed, failedReason: "failed to handle claim");
            _unitOfWork.Commit();
            _claimMetrics.IncrementFailedClaims();
        }
    }

    /// <summary>
    /// This method builds a routing slip with a greedy algorithm.
    /// It slips slices as required to claim the requested quantity.
    /// </summary>
    /// <param name="processBuilder"></param>
    /// <param name="quantity">The quantity to claim</param>
    /// <param name="reservedConsumptionSlices">List of slices on consumption certificates</param>
    /// <param name="reservedProductionSlices">List of slices on production certificates</param>
    /// <param name="requestStatusArgs"></param>
    /// <returns></returns>
    private static async Task<RoutingSlip> BuildClaimRoutingSlip(IRegistryProcessBuilder processBuilder, long quantity, IList<WalletSlice> reservedConsumptionSlices, IList<WalletSlice> reservedProductionSlices, RequestStatusArgs requestStatusArgs)
    {
        var remainderToClaim = quantity;
        WalletSlice? productionRemainderSlice = null;

        foreach (var loopConsumptionSlice in reservedConsumptionSlices)
        {
            WalletSlice? consumptionRemainderSlice = loopConsumptionSlice;
            while (consumptionRemainderSlice is not null)
            {
                if (productionRemainderSlice is null)
                {
                    productionRemainderSlice = reservedProductionSlices.PopFirst();
                }

                if (consumptionRemainderSlice.Quantity > remainderToClaim)
                {
                    var (quantitySlice, remainderSlice) = await processBuilder.SplitSlice(consumptionRemainderSlice, remainderToClaim, requestStatusArgs);
                    processBuilder.SetWalletSliceStates(new() { { remainderSlice.Id, WalletSliceState.Available } }, requestStatusArgs);
                    consumptionRemainderSlice = quantitySlice;
                }

                if (productionRemainderSlice.Quantity < consumptionRemainderSlice.Quantity)
                {
                    var (quantitySlice, remainderSlice) = await processBuilder.SplitSlice(consumptionRemainderSlice, productionRemainderSlice.Quantity, requestStatusArgs);
                    await processBuilder.Claim(productionRemainderSlice, quantitySlice);

                    remainderToClaim -= quantitySlice.Quantity;
                    productionRemainderSlice = null; // production slice is fully claimed
                    consumptionRemainderSlice = remainderSlice;
                }
                else if (productionRemainderSlice.Quantity > consumptionRemainderSlice.Quantity)
                {
                    var (quantitySlice, remainderSlice) = await processBuilder.SplitSlice(productionRemainderSlice, consumptionRemainderSlice.Quantity, requestStatusArgs);
                    await processBuilder.Claim(quantitySlice, consumptionRemainderSlice);

                    remainderToClaim -= consumptionRemainderSlice.Quantity;
                    productionRemainderSlice = remainderSlice;
                    consumptionRemainderSlice = null; // consumption slice is fully claimed
                }
                else // productionRemainderSlice.Quantity == consumptionRemainderSlice.Quantity
                {
                    await processBuilder.Claim(productionRemainderSlice, consumptionRemainderSlice);

                    remainderToClaim -= consumptionRemainderSlice.Quantity;
                    productionRemainderSlice = null; // production slice is fully claimed
                    consumptionRemainderSlice = null; // consumption slice is fully claimed
                }
            }
        }

        if (productionRemainderSlice is not null)
        {
            // if last production slice has remainder, it should be returned to the available
            processBuilder.SetWalletSliceStates(new() { { productionRemainderSlice.Id, WalletSliceState.Available } }, requestStatusArgs);
        }

        return processBuilder.Build();
    }
}
