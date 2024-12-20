using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using ProjectOrigin.Vault.Database;
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

    public UpdateClaimStateActivity(IUnitOfWork unitOfWork, ILogger<UpdateClaimStateActivity> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ExecutionResult> Execute(ExecuteContext<UpdateClaimStateArguments> context)
    {
        _logger.LogDebug("RoutingSlip {TrackingNumber} - Executing {ActivityName}", context.TrackingNumber, context.ActivityName);

        try
        {
            await _unitOfWork.ClaimRepository.SetClaimState(context.Arguments.Id, context.Arguments.State);

            if (context.Arguments.RequestStatusArgs != null)
            {
                await _unitOfWork.RequestStatusRepository.SetRequestStatus(context.Arguments.RequestStatusArgs.RequestId, context.Arguments.RequestStatusArgs.Owner, RequestStatusState.Completed);
            }

            _unitOfWork.Commit();
            return context.Completed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while updating claim state");
            _unitOfWork.Rollback();
            return context.Faulted(ex);
        }
    }
}
