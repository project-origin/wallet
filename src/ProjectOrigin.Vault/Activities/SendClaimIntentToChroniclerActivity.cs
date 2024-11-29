using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Net.Client;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Chronicler.V1;
using ProjectOrigin.Vault.Activities.Exceptions;
using ProjectOrigin.Vault.Options;

namespace ProjectOrigin.Vault.Activities;

public record SendClaimIntentToChroniclerArgument
{
    public required Guid Id { get; init; }
    public required ClaimIntentRequest ClaimIntentRequest { get; init; }
}

public class SendClaimIntentToChroniclerActivity : IExecuteActivity<SendClaimIntentToChroniclerArgument>
{
    private readonly ILogger<SendClaimIntentToChroniclerActivity> _logger;
    private readonly IOptions<NetworkOptions> _networkOptions;
    private readonly Func<GrpcChannel, ChroniclerService.ChroniclerServiceClient> _factory;

    public SendClaimIntentToChroniclerActivity(
        IOptions<NetworkOptions> networkOptions,
        ILogger<SendClaimIntentToChroniclerActivity> logger
        )
    {
        _logger = logger;
        _networkOptions = networkOptions;
        _factory = channel => new ChroniclerService.ChroniclerServiceClient(channel);
    }

    public SendClaimIntentToChroniclerActivity(
        IOptions<NetworkOptions> networkOptions,
        ILogger<SendClaimIntentToChroniclerActivity> logger,
        Func<GrpcChannel, ChroniclerService.ChroniclerServiceClient> factory
        )
    {
        _logger = logger;
        _networkOptions = networkOptions;
        _factory = factory;
    }

    public async Task<ExecutionResult> Execute(ExecuteContext<SendClaimIntentToChroniclerArgument> context)
    {
        _logger.LogDebug("RoutingSlip {TrackingNumber} - Executing {ActivityName}", context.TrackingNumber, context.ActivityName);

        try
        {
            var claimIntentRequest = context.Arguments.ClaimIntentRequest;

            var registryName = claimIntentRequest.CertificateId.Registry;
            if (!_networkOptions.Value.Registries.TryGetValue(registryName, out var registryInfo))
                throw new ArgumentException($"Registry with name {registryName} not found in configuration.");

            using var channel = GrpcChannel.ForAddress(registryInfo.Url);

            var client = _factory(channel);

            var result = await client.RegisterClaimIntentAsync(claimIntentRequest);

            _logger.LogDebug("Claim intent registered with Chronicler");

            Dictionary<string, object> variables = new() {
                { context.Arguments.Id.ToString(), result.Signature.ToByteArray() }
            };

            return context.CompletedWithVariables(variables);
        }
        catch (Exception ex)
        {
            throw new ChroniclerException("Error registering claim intent with Chronicler", ex);
        }
    }
}
