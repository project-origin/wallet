using Grpc.Core;
using Grpc.Net.Client;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.Vault.Activities.Exceptions;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Exceptions;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Options;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProjectOrigin.Vault.EventHandlers;

public record TransferFullSliceWaitCommittedTransactionArguments
{
    public required string RegistryName { get; set; }
    public required string TransactionId { get; set; }
    public required Guid CertificateId { get; set; }
    public required Guid SliceId { get; set; }
    public required Guid TransferredSliceId { get; set; }
    public required WalletAttribute[] WalletAttributes { get; set; }
    public required Guid ExternalEndpointId { get; set; }
    public RequestStatusArgs? RequestStatusArgs { get; set; }
}

public record TransferPartialSliceWaitCommittedTransactionArguments
{
    public required string RegistryName { get; set; }
    public required string TransactionId { get; set; }
    public required Guid CertificateId { get; set; }
    public required Guid SourceSliceId { get; set; }
    public required Guid RemainderSliceId { get; set; }
    public required Guid TransferredSliceId { get; set; }
    public required WalletAttribute[] WalletAttributes { get; set; }
    public required Guid ExternalEndpointId { get; set; }
    public RequestStatusArgs? RequestStatusArgs { get; set; }
}

public class VaultWaitCommittedRegistryTransactionConsumer : IConsumer<TransferFullSliceWaitCommittedTransactionArguments>,
    IConsumer<TransferPartialSliceWaitCommittedTransactionArguments>
{
    private readonly IOptions<NetworkOptions> _networkOptions;
    private readonly ILogger<VaultWaitCommittedRegistryTransactionConsumer> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimMetrics _claimMetrics;
    private readonly ITransferMetrics _transferMetrics;

    public VaultWaitCommittedRegistryTransactionConsumer(IOptions<NetworkOptions> networkOptions, ILogger<VaultWaitCommittedRegistryTransactionConsumer> logger, IUnitOfWork unitOfWork, IClaimMetrics claimMetrics, ITransferMetrics transferMetrics)
    {
        _networkOptions = networkOptions;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _claimMetrics = claimMetrics;
        _transferMetrics = transferMetrics;
    }

    public async Task Consume(ConsumeContext<TransferFullSliceWaitCommittedTransactionArguments> context)
    {
        var msg = context.Message;

        if (msg.RequestStatusArgs != null)
        {
            _logger.LogInformation("Starting consumer: {Consumer}, RequestId: {RequestId} ", nameof(VaultWaitCommittedRegistryTransactionConsumer), msg.RequestStatusArgs.RequestId);
        }

        await WaitForCommittedTransaction(msg.TransactionId,
            msg.RegistryName,
            new Dictionary<Guid, WalletSliceState> { { msg.SliceId, WalletSliceState.Sliced } },
            msg.CertificateId,
            msg.RequestStatusArgs,
            async () =>
                await _unitOfWork.OutboxMessageRepository.Create(new OutboxMessage
                {
                    Created = DateTimeOffset.UtcNow.ToUtcTime(),
                    Id = Guid.NewGuid(),
                    MessageType = typeof(SendTransferSliceInformationToReceiverWalletArgument).ToString(),
                    JsonPayload = JsonSerializer.Serialize(new SendTransferSliceInformationToReceiverWalletArgument
                    {
                        RequestStatusArgs = msg.RequestStatusArgs!,
                        SliceId = msg.TransferredSliceId,
                        WalletAttributes = msg.WalletAttributes,
                        ExternalEndpointId = msg.ExternalEndpointId
                    })
                })
        );
    }

    public async Task Consume(ConsumeContext<TransferPartialSliceWaitCommittedTransactionArguments> context)
    {
        var msg = context.Message;

        if (msg.RequestStatusArgs != null)
        {
            _logger.LogInformation("Starting consumer: {Consumer}, RequestId: {RequestId} ", nameof(VaultWaitCommittedRegistryTransactionConsumer), msg.RequestStatusArgs.RequestId);
        }

        await WaitForCommittedTransaction(msg.TransactionId,
            msg.RegistryName,
            new Dictionary<Guid, WalletSliceState>
            {
                { msg.SourceSliceId, WalletSliceState.Sliced },
                { msg.RemainderSliceId, WalletSliceState.Available }
            },
            msg.CertificateId,
            msg.RequestStatusArgs,
            async () =>
                await _unitOfWork.OutboxMessageRepository.Create(new OutboxMessage
                {
                    Created = DateTimeOffset.UtcNow.ToUtcTime(),
                    Id = Guid.NewGuid(),
                    MessageType = typeof(SendTransferSliceInformationToReceiverWalletArgument).ToString(),
                    JsonPayload = JsonSerializer.Serialize(new SendTransferSliceInformationToReceiverWalletArgument
                    {
                        RequestStatusArgs = msg.RequestStatusArgs!,
                        SliceId = msg.TransferredSliceId,
                        WalletAttributes = msg.WalletAttributes,
                        ExternalEndpointId = msg.ExternalEndpointId
                    })
                })
        );
    }

    private async Task WaitForCommittedTransaction(string transactionId,
        string registryName,
        Dictionary<Guid, WalletSliceState> sliceStates,
        Guid certificateId,
        RequestStatusArgs? requestStatusArgs,
        Func<Task> publishFunc)
    {
        try
        {
            var statusRequest = new GetTransactionStatusRequest
            {
                Id = transactionId
            };

            if (!_networkOptions.Value.Registries.TryGetValue(registryName, out var registryInfo))
                throw new ArgumentException($"Registry with name {registryName} not found in configuration.");

            using var channel = GrpcChannel.ForAddress(registryInfo.Url);

            var client = new RegistryService.RegistryServiceClient(channel);
            var status = await client.GetTransactionStatusAsync(statusRequest);

            if (status.Status == TransactionState.Committed)
            {
                if (requestStatusArgs != null)
                {
                    _logger.LogInformation("Ending consumer: {Consumer}, RequestId: {RequestId} ", nameof(VaultWaitCommittedRegistryTransactionConsumer), requestStatusArgs.RequestId);
                }

                foreach (var slice in sliceStates)
                {
                    await _unitOfWork.CertificateRepository.SetWalletSliceState(slice.Key, slice.Value);
                }

                await publishFunc();
                _unitOfWork.Commit();
            }
            else if (status.Status == TransactionState.Failed)
            {
                _logger.LogCritical("Transaction failed on registry. Certificate id {certificateId}. Message: {message}", certificateId, status.Message);
                if (requestStatusArgs != null)
                {
                    await _unitOfWork.RequestStatusRepository.SetRequestStatus(requestStatusArgs.RequestId, requestStatusArgs.Owner, RequestStatusState.Failed, failedReason: "Transaction failed on registry.");
                    _unitOfWork.Commit();

                    if (requestStatusArgs.RequestStatusType == RequestStatusType.Claim)
                    {
                        _claimMetrics.IncrementFailedClaims();
                    }
                }
            }
            else
            {
                _logger.LogDebug("Transaction is still processing on registry.");
                throw new RegistryTransactionStillProcessingException("Transaction is still processing on registry.");
            }
        }
        catch (RegistryTransactionStillProcessingException)
        {
            throw;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Failed to communicate with registry.");
            _unitOfWork.Rollback();
            throw new TransientException("Failed to communicate with registry.", ex);
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "Failed to communicate with the database.");
            throw new TransientException("Failed to communicate with the database.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get requestStatus from registry.");
            _unitOfWork.Rollback();
            if (requestStatusArgs != null)
            {
                await _unitOfWork.RequestStatusRepository.SetRequestStatus(requestStatusArgs.RequestId, requestStatusArgs.Owner, RequestStatusState.Failed, failedReason: "General error. Failed to get requestStatus from registry.");
                _unitOfWork.Commit();

                if (requestStatusArgs.RequestStatusType == RequestStatusType.Claim)
                {
                    _claimMetrics.IncrementFailedClaims();
                }
                else if (requestStatusArgs.RequestStatusType == RequestStatusType.Transfer)
                {
                    _transferMetrics.IncrementFailedTransfers();
                }
            }
            throw;
        }
    }
}
