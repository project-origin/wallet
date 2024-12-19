using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Models;

namespace ProjectOrigin.Vault.Activities;

public record UpdateSliceStateArguments()
{
    public required Dictionary<Guid, WalletSliceState> SliceStates { get; init; }
    public RequestStatusArgs? RequestStatusArgs { get; set; }
}

public class UpdateSliceStateActivity : IExecuteActivity<UpdateSliceStateArguments>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateSliceStateActivity> _logger;

    public UpdateSliceStateActivity(IUnitOfWork unitOfWork, ILogger<UpdateSliceStateActivity> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ExecutionResult> Execute(ExecuteContext<UpdateSliceStateArguments> context)
    {
        if (context.Arguments.RequestStatusArgs != null)
        {
            _logger.LogInformation("Starting Activity: {Activity}, RequestId: {RequestId} ", nameof(UpdateSliceStateActivity), context.Arguments.RequestStatusArgs.RequestId);
        }

        _logger.LogDebug("RoutingSlip {TrackingNumber} - Executing {ActivityName}", context.TrackingNumber, context.ActivityName);

        try
        {

            foreach (var (id, state) in context.Arguments.SliceStates)
            {
                await _unitOfWork.CertificateRepository.SetWalletSliceState(id, state);
            }
            _unitOfWork.Commit();
            if (context.Arguments.RequestStatusArgs != null)
            {
                _logger.LogInformation("Ending Activity: {Activity}, RequestId: {RequestId} ", nameof(UpdateSliceStateActivity), context.Arguments.RequestStatusArgs.RequestId);
            }

            return context.Completed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while updating slice state");
            throw;
        }
    }
}
