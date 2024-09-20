using System;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.Vault.Activities.Exceptions;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Options;

namespace ProjectOrigin.Vault.Activities;

public record WaitCommittedTransactionArguments
{
    public required string RegistryName { get; set; }
    public required string TransactionId { get; set; }
    public required Guid RequestId { get; set; }
    public required string Owner { get; set; }
    public required Guid CertificateId { get; set; }
    public required Guid SliceId { get; set; }
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
                _logger.LogCritical("Transaction failed on registry. Certificate id {certificateId}, slice id: {sliceId}. Message: {message}", context.Arguments.CertificateId, context.Arguments.SliceId, status.Message);
                await _unitOfWork.RequestStatusRepository.SetRequestStatus(context.Arguments.RequestId, context.Arguments.Owner, RequestStatusState.Failed, failedReason: "Transaction failed on registry.");
                _unitOfWork.Commit();
                return context.Faulted(new InvalidRegistryTransactionException($"Transaction failed on registry. Certificate id {context.Arguments.CertificateId}, slice id: {context.Arguments.SliceId}. Message: {status.Message}"));
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
            _logger.LogError(ex, "Failed to get requestStatus from registry.");
            await _unitOfWork.RequestStatusRepository.SetRequestStatus(context.Arguments.RequestId, context.Arguments.Owner, RequestStatusState.Failed, failedReason: "General error. Failed to get requestStatus from registry.");
            _unitOfWork.Commit();
            return context.Faulted(ex);
        }
    }
}
