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
}

public class UpdateClaimStateActivity : IExecuteActivity<UpdateClaimStateArguments>
{
    private IUnitOfWork _unitOfWork;
    private ILogger<UpdateClaimStateActivity> _logger;

    public UpdateClaimStateActivity(IUnitOfWork unitOfWork, ILogger<UpdateClaimStateActivity> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ExecutionResult> Execute(ExecuteContext<UpdateClaimStateArguments> context)
    {
        _logger.LogTrace("RoutingSlip {TrackingNumber} - Executing {ActivityName}", context.TrackingNumber, context.ActivityName);

        try
        {
            await _unitOfWork.CertificateRepository.SetClaimState(context.Arguments.Id, context.Arguments.State);
            _unitOfWork.Commit();
            return context.Completed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while updating slice state");
            throw;
        }
    }
}
