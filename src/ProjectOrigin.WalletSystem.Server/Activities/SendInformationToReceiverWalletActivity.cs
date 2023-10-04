using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Net.Client;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Options;

namespace ProjectOrigin.WalletSystem.Server.Activities;

public record SendInformationToReceiverWalletArgument
{
    public required Guid ExternalEndpointsId { get; init; }
    public required Guid SliceId { get; init; }
}

public class SendInformationToReceiverWalletActivity : IExecuteActivity<SendInformationToReceiverWalletArgument>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptions<ServiceOptions> _walletSystemOptions;
    private readonly ILogger<SendInformationToReceiverWalletActivity> _logger;

    public SendInformationToReceiverWalletActivity(IUnitOfWork unitOfWork, IOptions<ServiceOptions> walletSystemOptions, ILogger<SendInformationToReceiverWalletActivity> logger)
    {
        _unitOfWork = unitOfWork;
        _walletSystemOptions = walletSystemOptions;
        _logger = logger;
    }

    public async Task<ExecutionResult> Execute(ExecuteContext<SendInformationToReceiverWalletArgument> context)
    {
        _logger.LogTrace("RoutingSlip {TrackingNumber} - Executing {ActivityName}", context.TrackingNumber, context.ActivityName);

        var newSlice = await _unitOfWork.CertificateRepository.GetTransferredSlice(context.Arguments.SliceId);
        var externalEndpoints = await _unitOfWork.WalletRepository.GetExternalEndpoints(context.Arguments.ExternalEndpointsId);

        if (_walletSystemOptions.Value.EndpointAddress == externalEndpoints.Endpoint)
        {
            return await InsertIntoLocalWallet(context, newSlice, externalEndpoints);
        }
        else
        {
            return await SendOverGrpcToExternalWallet(context, newSlice, externalEndpoints);
        }
    }

    private async Task<ExecutionResult> SendOverGrpcToExternalWallet(ExecuteContext<SendInformationToReceiverWalletArgument> context, TransferredSlice newSlice, ExternalEndpoints externalEndpoints)
    {
        try
        {
            _logger.LogTrace("Preparing to send information to receiver");

            var request = new V1.ReceiveRequest
            {
                WalletDepositEndpointPublicKey = ByteString.CopyFrom(externalEndpoints.PublicKey.Export()),
                WalletDepositEndpointPosition = (uint)newSlice.ExternalEndpointsPosition,
                CertificateId = newSlice.GetFederatedStreamId(),
                Quantity = (uint)newSlice.Quantity,
                RandomR = ByteString.CopyFrom(newSlice.RandomR)
            };

            using var channel = GrpcChannel.ForAddress(externalEndpoints.Endpoint);
            var client = new V1.ReceiveSliceService.ReceiveSliceServiceClient(channel);

            _logger.LogTrace("Sending information to receiver");
            await client.ReceiveSliceAsync(request);
            await _unitOfWork.CertificateRepository.SetTransferredSliceState(newSlice.Id, TransferredSliceState.Transferred);


            _logger.LogTrace("Information Sent to receiver");

            return context.Completed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send information to receiver");
            throw;
        }
    }

    private async Task<ExecutionResult> InsertIntoLocalWallet(ExecuteContext<SendInformationToReceiverWalletArgument> context, TransferredSlice newSlice, ExternalEndpoints externalEndpoints)
    {
        _logger.LogTrace("Receiver is local.");

        var endpoint = await _unitOfWork.WalletRepository.GetWalletEndpoint(externalEndpoints.PublicKey);

        if (endpoint is null)
        {
            _logger.LogError("Local receiver wallet could not be found for reciever wallet {ReceiverWalletId}", externalEndpoints.Id);
            return context.Faulted(new Exception($"Local receiver wallet could not be found for reciever wallet {externalEndpoints.Id}"));
        }

        var slice = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = newSlice.ExternalEndpointsPosition,
            RegistryName = newSlice.RegistryName,
            CertificateId = newSlice.CertificateId,
            Quantity = newSlice.Quantity,
            RandomR = newSlice.RandomR,
            SliceState = WalletSliceState.Available
        };
        await _unitOfWork.CertificateRepository.InsertWalletSlice(slice);
        await _unitOfWork.CertificateRepository.SetTransferredSliceState(newSlice.Id, TransferredSliceState.Transferred);
        _unitOfWork.Commit();

        _logger.LogTrace("Slice inserted locally into receiver wallet.");

        return context.Completed();
    }
}
