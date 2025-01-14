using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Options;
using ProjectOrigin.Vault.Services.REST.v1;

namespace ProjectOrigin.Vault.Activities;

public record SendInformationToReceiverWalletArgument
{
    public required Guid ExternalEndpointId { get; init; }
    public required Guid SliceId { get; init; }
    public required WalletAttribute[] WalletAttributes { get; init; }
    public required Guid RequestId { get; init; }
    public required string Owner { get; init; }
}

public class SendInformationToReceiverWalletActivity : IExecuteActivity<SendInformationToReceiverWalletArgument>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SendInformationToReceiverWalletActivity> _logger;
    private readonly Uri _ownEndpoint;
    private readonly ITransferMetrics _transferMetrics;

    public SendInformationToReceiverWalletActivity(IUnitOfWork unitOfWork, IOptions<ServiceOptions> walletSystemOptions, ILogger<SendInformationToReceiverWalletActivity> logger, ITransferMetrics transferMetrics)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _ownEndpoint = new Uri(walletSystemOptions.Value.EndpointAddress, "/v1/slices");
        _transferMetrics = transferMetrics;
    }

    public async Task<ExecutionResult> Execute(ExecuteContext<SendInformationToReceiverWalletArgument> context)
    {
        _logger.LogDebug("RoutingSlip {TrackingNumber} - Executing {ActivityName}", context.TrackingNumber, context.ActivityName);
        _logger.LogInformation("Starting Activity: {Activity}, RequestId: {RequestId} ",
            nameof(SendInformationToReceiverWalletActivity), context.Arguments.RequestId);

        var newSlice = await _unitOfWork.TransferRepository.GetTransferredSlice(context.Arguments.SliceId);
        var externalEndpoint = await _unitOfWork.WalletRepository.GetExternalEndpoint(context.Arguments.ExternalEndpointId);

        if (externalEndpoint.Endpoint.Equals(_ownEndpoint.ToString()))
        {
            _logger.LogInformation("Sending to local wallet.");
            return await InsertIntoLocalWallet(context, newSlice, externalEndpoint);
        }
        else
        {
            _logger.LogInformation("Sending to external wallet.");
            return await SendOverRestToExternalWallet(context, newSlice, externalEndpoint);
        }
    }

    private async Task<ExecutionResult> SendOverRestToExternalWallet(
        ExecuteContext<SendInformationToReceiverWalletArgument> context,
        TransferredSlice newSlice,
        ExternalEndpoint externalEndpoint)
    {
        try
        {
            _logger.LogInformation("Preparing to send information to receiver");

            var request = new ReceiveRequest
            {
                PublicKey = externalEndpoint.PublicKey.Export().ToArray(),
                Position = (uint)newSlice.ExternalEndpointPosition,
                CertificateId = new FederatedStreamId
                {
                    Registry = newSlice.RegistryName,
                    StreamId = newSlice.CertificateId
                },
                Quantity = (uint)newSlice.Quantity,
                RandomR = newSlice.RandomR,
                HashedAttributes = context.Arguments.WalletAttributes.Select(ha =>
                    new HashedAttribute
                    {
                        Key = ha.Key,
                        Value = ha.Value,
                        Salt = ha.Salt,
                    })
            };

            var client = new HttpClient();
            _logger.LogInformation("Sending information to receiver");

            var response = await client.PostAsJsonAsync(externalEndpoint.Endpoint, request);
            response.EnsureSuccessStatusCode();
            await _unitOfWork.TransferRepository.SetTransferredSliceState(newSlice.Id, TransferredSliceState.Transferred);
            await _unitOfWork.RequestStatusRepository.SetRequestStatus(context.Arguments.RequestId, context.Arguments.Owner, RequestStatusState.Completed);

            _unitOfWork.Commit();
            _transferMetrics.IncrementCompleted();

            _logger.LogInformation("Information Sent to receiver");
            _logger.LogInformation("Ending ExternalWallet Activity: {Activity}, RequestId: {RequestId} ", nameof(SendInformationToReceiverWalletActivity), context.Arguments.RequestId);

            return context.Completed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send information to receiver");
            throw;
        }
    }

    private async Task<ExecutionResult> InsertIntoLocalWallet(ExecuteContext<SendInformationToReceiverWalletArgument> context, TransferredSlice newSlice, ExternalEndpoint externalEndpoint)
    {
        _logger.LogInformation("Receiver is local.");

        var walletEndpoint = await _unitOfWork.WalletRepository.GetWalletEndpoint(externalEndpoint.PublicKey);

        if (walletEndpoint is null)
        {
            _logger.LogError("Local receiver wallet could not be found for reciever wallet {ReceiverWalletId}", externalEndpoint.Id);
            return context.Faulted(new Exception($"Local receiver wallet could not be found for reciever wallet {externalEndpoint.Id}"));
        }

        var slice = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = walletEndpoint.Id,
            WalletEndpointPosition = newSlice.ExternalEndpointPosition,
            RegistryName = newSlice.RegistryName,
            CertificateId = newSlice.CertificateId,
            Quantity = newSlice.Quantity,
            RandomR = newSlice.RandomR,
            State = WalletSliceState.Available
        };
        await _unitOfWork.CertificateRepository.InsertWalletSlice(slice);
        await _unitOfWork.TransferRepository.SetTransferredSliceState(newSlice.Id, TransferredSliceState.Transferred);
        foreach (var walletAttribute in context.Arguments.WalletAttributes)
        {
            await _unitOfWork.CertificateRepository.InsertWalletAttribute(walletEndpoint.WalletId, walletAttribute);
        }
        await _unitOfWork.RequestStatusRepository.SetRequestStatus(context.Arguments.RequestId, context.Arguments.Owner, RequestStatusState.Completed);

        _unitOfWork.Commit();
        _transferMetrics.IncrementCompleted();

        _logger.LogInformation("Slice inserted locally into receiver wallet.");
        _logger.LogInformation("Ending IntoLocalWallet Activity: {Activity}, RequestId: {RequestId} ", nameof(SendInformationToReceiverWalletActivity), context.Arguments.RequestId);
        return context.Completed();
    }
}
