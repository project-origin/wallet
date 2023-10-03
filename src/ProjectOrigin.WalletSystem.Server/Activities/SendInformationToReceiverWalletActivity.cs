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
    public required Guid OutboxEndpointId { get; init; }
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

        var newSlice = await _unitOfWork.CertificateRepository.GetOutboxSlice(context.Arguments.SliceId);
        var outboxEndpoint = await _unitOfWork.WalletRepository.GetOutboxEndpoint(context.Arguments.OutboxEndpointId);

        if (_walletSystemOptions.Value.EndpointAddress == outboxEndpoint.Endpoint)
        {
            return await InsertIntoLocalWallet(context, newSlice, outboxEndpoint);
        }
        else
        {
            return await SendOverGrpcToExternalWallet(context, newSlice, outboxEndpoint);
        }
    }

    private async Task<ExecutionResult> SendOverGrpcToExternalWallet(ExecuteContext<SendInformationToReceiverWalletArgument> context, OutboxSlice newSlice, OutboxEndpoint outboxEndpoint)
    {
        try
        {
            _logger.LogTrace("Preparing to send information to receiver");

            var request = new V1.ReceiveRequest
            {
                WalletDepositEndpointPublicKey = ByteString.CopyFrom(outboxEndpoint.PublicKey.Export()),
                WalletDepositEndpointPosition = (uint)newSlice.OutboxEndpointPosition,
                CertificateId = newSlice.GetFederatedStreamId(),
                Quantity = (uint)newSlice.Quantity,
                RandomR = ByteString.CopyFrom(newSlice.RandomR)
            };

            using var channel = GrpcChannel.ForAddress(outboxEndpoint.Endpoint);
            var client = new V1.ReceiveSliceService.ReceiveSliceServiceClient(channel);

            _logger.LogTrace("Sending information to receiver");
            await client.ReceiveSliceAsync(request);
            await _unitOfWork.CertificateRepository.SetOutboxSliceState(newSlice.Id, OutboxSliceState.Transferred);


            _logger.LogTrace("Information Sent to receiver");

            return context.Completed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send information to receiver");
            throw;
        }
    }

    private async Task<ExecutionResult> InsertIntoLocalWallet(ExecuteContext<SendInformationToReceiverWalletArgument> context, OutboxSlice newSlice, OutboxEndpoint outboxEndpoint)
    {
        _logger.LogTrace("Receiver is local.");

        var endpoint = await _unitOfWork.WalletRepository.GetWalletEndpoint(outboxEndpoint.PublicKey);

        if (endpoint is null)
        {
            _logger.LogError("Local receiver wallet could not be found for reciever wallet {ReceiverWalletId}", outboxEndpoint.Id);
            return context.Faulted(new Exception($"Local receiver wallet could not be found for reciever wallet {outboxEndpoint.Id}"));
        }

        var slice = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = newSlice.OutboxEndpointPosition,
            RegistryName = newSlice.RegistryName,
            CertificateId = newSlice.CertificateId,
            Quantity = newSlice.Quantity,
            RandomR = newSlice.RandomR,
            SliceState = WalletSliceState.Available
        };
        await _unitOfWork.CertificateRepository.InsertWalletSlice(slice);
        await _unitOfWork.CertificateRepository.SetOutboxSliceState(newSlice.Id, OutboxSliceState.Transferred);
        _unitOfWork.Commit();

        _logger.LogTrace("Slice inserted locally into receiver wallet.");

        return context.Completed();
    }
}
