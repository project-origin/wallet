using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using Npgsql;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Exceptions;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Models;

namespace ProjectOrigin.Vault.Activities;

public record UpdateSliceStateArguments()
{
    public required Dictionary<Guid, WalletSliceState> SliceStates { get; init; }
    public required RequestStatusArgs? RequestStatusArgs { get; set; }
}

public class UpdateSliceStateActivity : IExecuteActivity<UpdateSliceStateArguments>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateSliceStateActivity> _logger;
    private readonly IClaimMetrics _claimMetrics;
    private readonly ITransferMetrics _transferMetrics;

    public UpdateSliceStateActivity(IUnitOfWork unitOfWork, ILogger<UpdateSliceStateActivity> logger, IClaimMetrics claimMetrics, ITransferMetrics transferMetrics)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _claimMetrics = claimMetrics;
        _transferMetrics = transferMetrics;
    }

    public async Task<ExecutionResult> Execute(ExecuteContext<UpdateSliceStateArguments> context)
    {
        _logger.LogDebug("RoutingSlip {TrackingNumber} - Executing {ActivityName}", context.TrackingNumber, context.ActivityName);

        try
        {
            foreach (var (id, state) in context.Arguments.SliceStates)
            {
                await _unitOfWork.CertificateRepository.SetWalletSliceState(id, state);
            }
            _unitOfWork.Commit();
            return context.Completed();
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "Failed to communicate with the database.");
            throw new TransientException("Failed to communicate with the database.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while updating slice state");
            _unitOfWork.Rollback();

            if (context.Arguments.RequestStatusArgs != null)
            {
                await _unitOfWork.RequestStatusRepository.SetRequestStatus(
                    context.Arguments.RequestStatusArgs.RequestId,
                    context.Arguments.RequestStatusArgs.Owner,
                    RequestStatusState.Failed,
                    "Error while updating slice state");

                _unitOfWork.Commit();

                if (context.Arguments.RequestStatusArgs.RequestStatusType == RequestStatusType.Claim)
                {
                    _claimMetrics.IncrementFailedClaims();
                }
                else if (context.Arguments.RequestStatusArgs.RequestStatusType == RequestStatusType.Transfer)
                {
                    _transferMetrics.IncrementFailedTransfers();
                }
            }
            throw;
        }
    }
}
