using System;
using System.Linq;
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
    public required Guid ExternalEndpointId { get; init; }
    public required Guid SliceId { get; init; }
    public required WalletAttribute[] WalletAttributes { get; init; }
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
        _logger.LogDebug("RoutingSlip {TrackingNumber} - Executing {ActivityName}", context.TrackingNumber, context.ActivityName);

        var newSlice = await _unitOfWork.TransferRepository.GetTransferredSlice(context.Arguments.SliceId);
        var externalEndpoint = await _unitOfWork.WalletRepository.GetExternalEndpoint(context.Arguments.ExternalEndpointId);

        if (_walletSystemOptions.Value.EndpointAddress.Equals(externalEndpoint.Endpoint))
        {
            return await InsertIntoLocalWallet(context, newSlice, externalEndpoint);
        }
        else
        {
            return await SendOverGrpcToExternalWallet(context, newSlice, externalEndpoint);
        }
    }

    private async Task<ExecutionResult> SendOverGrpcToExternalWallet(ExecuteContext<SendInformationToReceiverWalletArgument> context, TransferredSlice newSlice, ExternalEndpoint externalEndpoint)
    {
        try
        {
            _logger.LogDebug("Preparing to send information to receiver");

            var request = new V1.ReceiveRequest
            {
                WalletDepositEndpointPublicKey = ByteString.CopyFrom(externalEndpoint.PublicKey.Export()),
                WalletDepositEndpointPosition = (uint)newSlice.ExternalEndpointPosition,
                CertificateId = newSlice.GetFederatedStreamId(),
                Quantity = (uint)newSlice.Quantity,
                RandomR = ByteString.CopyFrom(newSlice.RandomR),
                HashedAttributes = {
                    context.Arguments.WalletAttributes.Select(ha =>
                        new V1.ReceiveRequest.Types.HashedAttribute
                        {
                            Key = ha.Key,
                            Value = ha.Value,
                            Salt = ByteString.CopyFrom(ha.Salt),
                        })
                }
            };

            using var channel = GrpcChannel.ForAddress(externalEndpoint.Endpoint);
            var client = new V1.ReceiveSliceService.ReceiveSliceServiceClient(channel);

            _logger.LogDebug("Sending information to receiver");
            await client.ReceiveSliceAsync(request);
            await _unitOfWork.TransferRepository.SetTransferredSliceState(newSlice.Id, TransferredSliceState.Transferred);


            _logger.LogDebug("Information Sent to receiver");

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
        _logger.LogDebug("Receiver is local.");

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

        _unitOfWork.Commit();

        _logger.LogDebug("Slice inserted locally into receiver wallet.");

        return context.Completed();
    }
}
