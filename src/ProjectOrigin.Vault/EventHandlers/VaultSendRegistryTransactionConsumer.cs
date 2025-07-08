using Grpc.Net.Client;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Options;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using ProjectOrigin.Vault.Database;

namespace ProjectOrigin.Vault.EventHandlers;

public record TransferFullSliceRegistryTransactionArguments
{
    public required Transaction Transaction { get; init; }
    public required string RegistryName { get; set; }
    public required Guid CertificateId { get; set; }
    public required Guid SliceId { get; set; }
    public required Guid TransferredSliceId { get; set; }
    public required WalletAttribute[] WalletAttributes { get; set; }
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VaultSendRegistryTransactionConsumer> _logger;

    public VaultSendRegistryTransactionConsumer(IOptions<NetworkOptions> networkOptions, IUnitOfWork unitOfWork, ILogger<VaultSendRegistryTransactionConsumer> logger)
    {
        _networkOptions = networkOptions;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TransferFullSliceRegistryTransactionArguments> context)
    {
        _logger.LogInformation("Starting consumer: {Consumer} with arguments {Args}", nameof(VaultSendRegistryTransactionConsumer), nameof(TransferFullSliceRegistryTransactionArguments));

        var msg = context.Message;

        try
        {
            await SendTransactionToRegistry(msg.Transaction);

            var full = new TransferFullSliceWaitCommittedTransactionArguments
            {
                CertificateId = msg.CertificateId,
                RegistryName = msg.RegistryName,
                SliceId = msg.SliceId,
                TransferredSliceId = msg.TransferredSliceId,
                TransactionId = msg.Transaction.ToShaId(),
                ExternalEndpointId = msg.ExternalEndpointId,
                RequestStatusArgs = msg.RequestStatusArgs,
                WalletAttributes = msg.WalletAttributes
            };
            await _unitOfWork.OutboxMessageRepository.Create(new OutboxMessage
            {
                Created = DateTimeOffset.UtcNow.ToUtcTime(),
                Id = Guid.NewGuid(),
                MessageType = typeof(TransferFullSliceWaitCommittedTransactionArguments).ToString(),
                JsonPayload = JsonSerializer.Serialize(full)
            });
            _unitOfWork.Commit();

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

            var partial = new TransferPartialSliceWaitCommittedTransactionArguments
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
            };
            await _unitOfWork.OutboxMessageRepository.Create(new OutboxMessage
            {
                Created = DateTimeOffset.UtcNow.ToUtcTime(),
                Id = Guid.NewGuid(),
                MessageType = typeof(TransferPartialSliceWaitCommittedTransactionArguments).ToString(),
                JsonPayload = JsonSerializer.Serialize(partial)
            });
            _unitOfWork.Commit();

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
