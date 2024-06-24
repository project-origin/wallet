using System;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.WalletSystem.Server.Activities.Exceptions;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Options;

namespace ProjectOrigin.WalletSystem.Server.Activities;

public record WaitCommittedTransactionArguments
{
    public required string RegistryName { get; set; }
    public required string TransactionId { get; set; }
    public required Guid RequestId { get; set; }
}

public class WaitCommittedRegistryTransactionActivity : IExecuteActivity<WaitCommittedTransactionArguments>
{
    private readonly IOptions<RegistryOptions> _registryOptions;
    private readonly ILogger<WaitCommittedRegistryTransactionActivity> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public WaitCommittedRegistryTransactionActivity(IOptions<RegistryOptions> registryOptions, ILogger<WaitCommittedRegistryTransactionActivity> logger, IUnitOfWork unitOfWork)
    {
        _registryOptions = registryOptions;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<ExecutionResult> Execute(ExecuteContext<WaitCommittedTransactionArguments> context)
    {
        _logger.LogDebug("RoutingSlip {TrackingNumber} - Executing {ActivityName}", context.TrackingNumber, context.ActivityName);

        try
        {
            var statusRequest = new GetTransactionStatusRequest
            {
                Id = context.Arguments.TransactionId
            };

            var registryUrl = _registryOptions.Value.RegistryUrls[context.Arguments.RegistryName];
            using var channel = GrpcChannel.ForAddress(registryUrl);

            var client = new RegistryService.RegistryServiceClient(channel);
            var status = await client.GetTransactionStatusAsync(statusRequest);

            if (status.Status == TransactionState.Committed)
            {
                return context.Completed();
            }
            else if (status.Status == TransactionState.Failed)
            {
                _logger.LogCritical("Transaction failed on registry. Message: {message}", status.Message);
                await _unitOfWork.RequestStatusRepository.SetRequestStatus(context.Arguments.RequestId, StatusState.Failed);
                return context.Faulted(new InvalidRegistryTransactionException($"Transaction failed on registry. Message: {status.Message}"));
            }
            else
            {
                _logger.LogDebug("Transaction is still processing on registry.");
                return context.Faulted(new RegistryTransactionStillProcessingException("Transaction is still processing on registry."));
            }
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Failed to communicate with registry.");
            return context.Faulted(new TransientException("Failed to communicate with registry.", ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status from registry.");
            return context.Faulted(ex);
        }
    }
}
