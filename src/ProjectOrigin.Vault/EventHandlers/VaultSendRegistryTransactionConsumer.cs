using Grpc.Net.Client;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.Vault.Options;
using System;
using System.Threading.Tasks;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Models;

namespace ProjectOrigin.Vault.EventHandlers;

public record TransferFullSliceRegistryTransactionArguments
{
    public required Transaction Transaction { get; init; }
    public required string RegistryName { get; set; }
    public required Guid CertificateId { get; set; }
    public required Guid SliceId { get; set; }
    public required WalletAttribute [] WalletAttributes { get; set; }
    public required Guid ExternalEndpointId { get; set; }
    public RequestStatusArgs? RequestStatusArgs { get; set; }
}

public record TransferPartialSliceRegistryTransactionArguments
{
    public required Transaction Transaction { get; init; }
    public required string RegistryName { get; set; }
    public required Guid CertificateId { get; set; }
    public required Guid TransferredSliceId { get; set; }
    public required Guid RemainderSliceId { get; set; }
    public required Guid SourceSliceId { get; set; }
    public required WalletAttribute[] WalletAttributes { get; set; }
    public required Guid ExternalEndpointId { get; set; }
    public RequestStatusArgs? RequestStatusArgs { get; set; }
}

public class VaultSendRegistryTransactionConsumer : IConsumer<TransferFullSliceRegistryTransactionArguments>,
    IConsumer<TransferPartialSliceRegistryTransactionArguments>
{
    private readonly IOptions<NetworkOptions> _networkOptions;
    private readonly ILogger<VaultSendRegistryTransactionConsumer> _logger;

    public VaultSendRegistryTransactionConsumer(IOptions<NetworkOptions> networkOptions, ILogger<VaultSendRegistryTransactionConsumer> logger)
    {
        _networkOptions = networkOptions;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TransferFullSliceRegistryTransactionArguments> context)
    {
        _logger.LogInformation("Starting consumer: {Consumer} with arguments {Args}", nameof(VaultSendRegistryTransactionConsumer), nameof(TransferFullSliceRegistryTransactionArguments));

        var msg = context.Message;

        try
        {
            await SendTransactionToRegistry(msg.Transaction);

            await context.Publish<TransferFullSliceWaitCommittedTransactionArguments>(new TransferFullSliceWaitCommittedTransactionArguments
            {
                CertificateId = msg.CertificateId,
                RegistryName = msg.RegistryName,
                SliceId = msg.SliceId,
                TransactionId = msg.Transaction.ToShaId(),
                ExternalEndpointId = msg.ExternalEndpointId,
                RequestStatusArgs = msg.RequestStatusArgs,
                WalletAttributes = msg.WalletAttributes
            });

            _logger.LogInformation("Ending consumer: {Consumer} with arguments {Args}", nameof(VaultSendRegistryTransactionConsumer), nameof(TransferFullSliceRegistryTransactionArguments));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending transactions to registry");
            throw;
        }
    }

    public async Task Consume(ConsumeContext<TransferPartialSliceRegistryTransactionArguments> context)
    {
        _logger.LogInformation("Starting consumer: {Consumer} with arguments {Args}", nameof(VaultSendRegistryTransactionConsumer), nameof(TransferPartialSliceRegistryTransactionArguments));

        var msg = context.Message;

        try
        {
            await SendTransactionToRegistry(msg.Transaction);

            await context.Publish<TransferPartialSliceWaitCommittedTransactionArguments>(new TransferPartialSliceWaitCommittedTransactionArguments
            {
                 CertificateId = msg.CertificateId,
                 RegistryName = msg.RegistryName,
                 SourceSliceId = msg.SourceSliceId,
                 TransferredSliceId = msg.TransferredSliceId,
                 RemainderSliceId = msg.RemainderSliceId,
                 TransactionId = msg.Transaction.ToShaId(),
                 ExternalEndpointId = msg.ExternalEndpointId,
                 RequestStatusArgs = msg.RequestStatusArgs,
                 WalletAttributes = msg.WalletAttributes
             });

            _logger.LogInformation("Ending consumer: {Consumer} with arguments {Args}", nameof(VaultSendRegistryTransactionConsumer), nameof(TransferPartialSliceRegistryTransactionArguments));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending transactions to registry");
            throw;
        }
    }

    private async Task SendTransactionToRegistry(Transaction transaction)
    {
        var registryName = transaction.Header.FederatedStreamId.Registry;

        var request = new SendTransactionsRequest();
        request.Transactions.Add(transaction);

        if (!_networkOptions.Value.Registries.TryGetValue(registryName, out var registryInfo))
            throw new ArgumentException($"Registry with name {registryName} not found in configuration.");

        using var channel = GrpcChannel.ForAddress(registryInfo.Url);

        var client = new RegistryService.RegistryServiceClient(channel);
        await client.SendTransactionsAsync(request);
    }
}
