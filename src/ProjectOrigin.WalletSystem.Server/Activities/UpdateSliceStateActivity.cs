using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Activities;

public record UpdateSliceStateArguments()
{
    public required Dictionary<Guid, WalletSliceState> SliceStates { get; init; }
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while updating slice state");
            throw;
        }
    }
}
