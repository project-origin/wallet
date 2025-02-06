using System;
using System.Threading.Tasks;
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
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Options;

namespace ProjectOrigin.Vault.Activities;

public record WaitCommittedTransactionArguments
{
    public required string RegistryName { get; set; }
    public required string TransactionId { get; set; }
    public required Guid CertificateId { get; set; }
    public required Guid SliceId { get; set; }
    public RequestStatusArgs? RequestStatusArgs { get; set; }
}

public class WaitCommittedRegistryTransactionActivity : IExecuteActivity<WaitCommittedTransactionArguments>
{
    private readonly IOptions<NetworkOptions> _networkOptions;
    private readonly ILogger<WaitCommittedRegistryTransactionActivity> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimMetrics _claimMetrics;

    public WaitCommittedRegistryTransactionActivity(IOptions<NetworkOptions> networkOptions, ILogger<WaitCommittedRegistryTransactionActivity> logger, IUnitOfWork unitOfWork, IClaimMetrics claimMetrics)
    {
        _networkOptions = networkOptions;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _claimMetrics = claimMetrics;
    }

    public async Task<ExecutionResult> Execute(ExecuteContext<WaitCommittedTransactionArguments> context)
    {
        _logger.LogDebug("RoutingSlip {TrackingNumber} - Executing {ActivityName}", context.TrackingNumber, context.ActivityName);
        if (context.Arguments.RequestStatusArgs != null)
        {
            _logger.LogInformation("Starting Activity: {Activity}, RequestId: {RequestId} ", nameof(WaitCommittedRegistryTransactionActivity), context.Arguments.RequestStatusArgs.RequestId);
        }

        try
        {
            var statusRequest = new GetTransactionStatusRequest
            {
                Id = context.Arguments.TransactionId
            };

            var registryName = context.Arguments.RegistryName;
            if (!_networkOptions.Value.Registries.TryGetValue(registryName, out var registryInfo))
                throw new ArgumentException($"Registry with name {registryName} not found in configuration.");

            using var channel = GrpcChannel.ForAddress(registryInfo.Url);

            var client = new RegistryService.RegistryServiceClient(channel);
            var status = await client.GetTransactionStatusAsync(statusRequest);

            if (status.Status == TransactionState.Committed)
            {
                if (context.Arguments.RequestStatusArgs != null)
                {
                    _logger.LogInformation("Ending Activity: {Activity}, RequestId: {RequestId} ", nameof(WaitCommittedRegistryTransactionActivity), context.Arguments.RequestStatusArgs.RequestId);
                }

                return context.Completed();
            }
            else if (status.Status == TransactionState.Failed)
            {
                _logger.LogCritical("Transaction failed on registry. Certificate id {certificateId}, slice id: {sliceId}. Message: {message}", context.Arguments.CertificateId, context.Arguments.SliceId, status.Message);
                if (context.Arguments.RequestStatusArgs != null)
                {
                    await _unitOfWork.RequestStatusRepository.SetRequestStatus(context.Arguments.RequestStatusArgs.RequestId, context.Arguments.RequestStatusArgs.Owner, RequestStatusState.Failed, failedReason: "Transaction failed on registry.");
                    _unitOfWork.Commit();

                    if (context.Arguments.RequestStatusArgs.RequestStatusType == RequestStatusType.Claim)
                    {
                        _claimMetrics.IncrementFailedClaims();
                    }
                }
                return context.Completed(new InvalidRegistryTransactionException($"Transaction failed on registry. Certificate id {context.Arguments.CertificateId}, slice id: {context.Arguments.SliceId}. Message: {status.Message}"));
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
            if (context.Arguments.RequestStatusArgs != null)
            {
                await _unitOfWork.RequestStatusRepository.SetRequestStatus(context.Arguments.RequestStatusArgs.RequestId, context.Arguments.RequestStatusArgs.Owner, RequestStatusState.Failed, failedReason: "General error. Failed to get requestStatus from registry.");
                _unitOfWork.Commit();

                if (context.Arguments.RequestStatusArgs.RequestStatusType == RequestStatusType.Claim)
                {
                    _claimMetrics.IncrementFailedClaims();
                }
            }
            return context.Completed(ex);
        }
    }
}
