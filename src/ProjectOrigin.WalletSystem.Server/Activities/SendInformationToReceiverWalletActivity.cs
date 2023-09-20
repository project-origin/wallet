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
    public required Guid ReceiverDepositEndpointId { get; init; }
    public required Guid SliceId { get; init; }
}

public class SendInformationToReceiverWalletActivity : IExecuteActivity<SendInformationToReceiverWalletArgument>
{
    private IUnitOfWork _unitOfWork;
    private IOptions<ServiceOptions> _walletSystemOptions;
    private ILogger<SendInformationToReceiverWalletActivity> _logger;

    public SendInformationToReceiverWalletActivity(IUnitOfWork unitOfWork, IOptions<ServiceOptions> walletSystemOptions, ILogger<SendInformationToReceiverWalletActivity> logger)
    {
        _unitOfWork = unitOfWork;
        _walletSystemOptions = walletSystemOptions;
        _logger = logger;
    }

    public async Task<ExecutionResult> Execute(ExecuteContext<SendInformationToReceiverWalletArgument> context)
    {
        _logger.LogTrace("RoutingSlip {TrackingNumber} - Executing {ActivityName}", context.TrackingNumber, context.ActivityName);

        var newSlice = await _unitOfWork.CertificateRepository.GetSlice(context.Arguments.SliceId);
        var receiverDepositEndpoint = await _unitOfWork.WalletRepository.GetDepositEndpoint(context.Arguments.ReceiverDepositEndpointId);

        if (_walletSystemOptions.Value.EndpointAddress == receiverDepositEndpoint.Endpoint)
        {
            return await InsertIntoLocalWallet(context, newSlice, receiverDepositEndpoint);
        }
        else
        {
            return await SendOverGrpcToExternalWallet(context, newSlice, receiverDepositEndpoint);
        }
    }

    private async Task<ExecutionResult> SendOverGrpcToExternalWallet(ExecuteContext<SendInformationToReceiverWalletArgument> context, Slice newSlice, DepositEndpoint receiverDepositEndpoint)
    {
        try
        {
            _logger.LogTrace("Preparing to send information to receiver");

            var request = new V1.ReceiveRequest
            {
                WalletDepositEndpointPublicKey = ByteString.CopyFrom(receiverDepositEndpoint.PublicKey.Export()),
                WalletDepositEndpointPosition = (uint)newSlice.DepositEndpointPosition,
                CertificateId = newSlice.GetFederatedStreamId(),
                Quantity = (uint)newSlice.Quantity,
                RandomR = ByteString.CopyFrom(newSlice.RandomR)
            };

            using var channel = GrpcChannel.ForAddress(receiverDepositEndpoint.Endpoint);
            var client = new V1.ReceiveSliceService.ReceiveSliceServiceClient(channel);

            _logger.LogTrace("Sending information to receiver");
            await client.ReceiveSliceAsync(request);

            _logger.LogTrace("Information Sent to receiver");

            return context.Completed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send information to receiver");
            throw;
        }
    }

    private async Task<ExecutionResult> InsertIntoLocalWallet(ExecuteContext<SendInformationToReceiverWalletArgument> context, Slice newSlice, DepositEndpoint receiverDepositEndpoint)
    {
        _logger.LogTrace("Receiver is local.");

        var receiverEndpoint = await _unitOfWork.WalletRepository.GetDepositEndpointFromPublicKey(receiverDepositEndpoint.PublicKey)
            ?? throw new Exception("Local receiver wallet could not be found");

        var slice = new Slice
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = receiverEndpoint.Id,
            DepositEndpointPosition = newSlice.DepositEndpointPosition,
            Registry = newSlice.Registry,
            CertificateId = newSlice.CertificateId,
            Quantity = newSlice.Quantity,
            RandomR = newSlice.RandomR,
            SliceState = SliceState.Available
        };

        await _unitOfWork.CertificateRepository.InsertSlice(slice);
        _unitOfWork.Commit();

        _logger.LogTrace("Slice inserted locally into receiver wallet.");

        return context.Completed();
    }
}
