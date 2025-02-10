using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using Npgsql;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Exceptions;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Models;

namespace ProjectOrigin.Vault.Activities;

public record UpdateClaimStateArguments()
{
    public required Guid Id { get; init; }
    public required ClaimState State { get; init; }
    public RequestStatusArgs? RequestStatusArgs { get; set; }
}

public class UpdateClaimStateActivity : IExecuteActivity<UpdateClaimStateArguments>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateClaimStateActivity> _logger;
    private readonly IClaimMetrics _claimsMetrics;

    public UpdateClaimStateActivity(IUnitOfWork unitOfWork, ILogger<UpdateClaimStateActivity> logger, IClaimMetrics claimsMetrics)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _claimsMetrics = claimsMetrics;
    }

    public async Task<ExecutionResult> Execute(ExecuteContext<UpdateClaimStateArguments> context)
    {
        _logger.LogDebug("RoutingSlip {TrackingNumber} - Executing {ActivityName}", context.TrackingNumber, context.ActivityName);
        if (context.Arguments.RequestStatusArgs != null)
        {
            _logger.LogInformation("Starting Activity: {Activity}, RequestId: {RequestId} ", nameof(UpdateClaimStateActivity), context.Arguments.RequestStatusArgs.RequestId);
        }

        try
        {
            await _unitOfWork.ClaimRepository.SetClaimState(context.Arguments.Id, context.Arguments.State);

            if (context.Arguments.RequestStatusArgs != null)
            {
                await _unitOfWork.RequestStatusRepository.SetRequestStatus(context.Arguments.RequestStatusArgs.RequestId, context.Arguments.RequestStatusArgs.Owner, RequestStatusState.Completed);
            }

            _unitOfWork.Commit();
            if (context.Arguments.RequestStatusArgs != null)
            {
                _logger.LogInformation("Ending Activity: {Activity}, RequestId: {RequestId} ", nameof(UpdateClaimStateActivity), context.Arguments.RequestStatusArgs.RequestId);
            }
            _claimsMetrics.IncrementClaimed();
            return context.Completed();
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "Failed to communicate with the database.");
            throw new TransientException("Failed to communicate with the database.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while updating claim state");
            _unitOfWork.Rollback();
            if (context.Arguments.RequestStatusArgs != null)
            {
                await _unitOfWork.RequestStatusRepository.SetRequestStatus(
                    context.Arguments.RequestStatusArgs.RequestId,
                    context.Arguments.RequestStatusArgs.Owner,
                    RequestStatusState.Failed,
                    "Error while updating claim state");

                _unitOfWork.Commit();
            }
            _claimsMetrics.IncrementFailedClaims();
            throw;
        }
    }
}
