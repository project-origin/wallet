using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.Vault.Options;

namespace ProjectOrigin.Vault.Activities;

public record SendRegistryTransactionArguments
{
    public required Transaction Transaction { get; init; }
}

public class SendRegistryTransactionActivity : IExecuteActivity<SendRegistryTransactionArguments>
{
    private readonly IOptions<NetworkOptions> _registryOptions;
    private readonly ILogger<SendRegistryTransactionActivity> _logger;

    public SendRegistryTransactionActivity(IOptions<NetworkOptions> registryOptions, ILogger<SendRegistryTransactionActivity> logger)
    {
        _registryOptions = registryOptions;
        _logger = logger;
    }

    public async Task<ExecutionResult> Execute(ExecuteContext<SendRegistryTransactionArguments> context)
    {
        _logger.LogDebug("RoutingSlip {TrackingNumber} - Executing {ActivityName}", context.TrackingNumber, context.ActivityName);

        try
        {
            var transaction = context.Arguments.Transaction;
            var registryName = transaction.Header.FederatedStreamId.Registry;

            var request = new SendTransactionsRequest();
            request.Transactions.Add(transaction);

            if (!_registryOptions.Value.Registries.TryGetValue(registryName, out var registryInfo))
                throw new ArgumentException($"Registry with name {registryName} not found in configuration.");

            using var channel = GrpcChannel.ForAddress(registryInfo.Url);

            var client = new RegistryService.RegistryServiceClient(channel);
            await client.SendTransactionsAsync(request);

            _logger.LogDebug("Transaction sent to registry");

            return context.Completed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending transactions to registry");
            return context.Faulted(ex);
        }
    }
}
