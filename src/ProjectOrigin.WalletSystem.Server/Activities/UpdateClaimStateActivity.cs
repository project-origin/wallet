using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Activities;

public record UpdateClaimStateArguments()
{
    public required Guid Id { get; init; }
    public required ClaimState State { get; init; }
    public required Guid RequestId { get; init; }
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
            await _unitOfWork.RequestStatusRepository.SetRequestStatus(context.Arguments.RequestId, RequestStatusState.Completed);
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
