using System;
using System.Threading.Tasks;
using Grpc.Core;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.Server.CommandHandlers;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.V1;

namespace ProjectOrigin.WalletSystem.Server.Services;

[AllowAnonymous]
public class ReceiveSliceService : V1.ReceiveSliceService.ReceiveSliceServiceBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHDAlgorithm _hdAlgorithm;
    private readonly IBus _bus;

    public ReceiveSliceService(IUnitOfWork unitOfWork, IHDAlgorithm hdAlgorithm, IBus bus)
    {
        _unitOfWork = unitOfWork;
        _hdAlgorithm = hdAlgorithm;
        _bus = bus;
    }

    public override async Task<ReceiveResponse> ReceiveSlice(ReceiveRequest request, ServerCallContext context)
    {
        var publicKey = _hdAlgorithm.ImportHDPublicKey(request.WalletDepositEndpointPublicKey.Span);
        var depositEndpoint = await _unitOfWork.WalletRepository.GetDepositEndpointFromPublicKey(publicKey);

        if (depositEndpoint == null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "DepositEndpoint not found for public key."));

        var newSliceCommand = new VerifySliceCommand(Guid.NewGuid(),
            depositEndpoint.Id,
            (int)request.WalletDepositEndpointPosition,
            request.CertificateId.Registry,
            Guid.Parse(request.CertificateId.StreamId.Value),
            request.Quantity,
            request.RandomR.ToByteArray());

        await _bus.Publish(newSliceCommand);

        return new ReceiveResponse();
    }
}
