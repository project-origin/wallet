using System;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.WalletSystem.Server.Activities.Exceptions;
using ProjectOrigin.WalletSystem.Server.Options;

namespace ProjectOrigin.WalletSystem.Server.Activities;

public record WaitCommittedTransactionArguments
{
    public required string RegistryName { get; set; }
    public required string TransactionId { get; set; }
}

public class WaitCommittedRegistryTransactionActivity : IExecuteActivity<WaitCommittedTransactionArguments>
{
    private readonly IOptions<RegistryOptions> _registryOptions;
    private readonly ILogger<WaitCommittedRegistryTransactionActivity> _logger;

    public WaitCommittedRegistryTransactionActivity(IOptions<RegistryOptions> registryOptions, ILogger<WaitCommittedRegistryTransactionActivity> logger)
    {
        _registryOptions = registryOptions;
        _logger = logger;
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
                var ex = new InvalidRegistryTransactionException($"Transaction failed on registry. Message: {status.Message}");
                _logger.LogCritical(ex, null);
                return context.Faulted(ex);
            }
            else
            {
                var message = "Transaction is still processing on registry.";
                _logger.LogDebug(message);
                return context.Faulted(new RegistryTransactionStillProcessingException(message));
            }
        }
        catch (RpcException ex)
        {
            var newEx = new TransientException("Failed to communicate with registry.", ex);
            _logger.LogError(newEx, null);
            return context.Faulted(newEx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status from registry.");
            return context.Faulted(ex);
        }
    }
}
