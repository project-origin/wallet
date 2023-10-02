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
        var endpoint = await _unitOfWork.WalletRepository.GetReceiveEndpoint(publicKey);

        if (endpoint == null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "DepositEndpoint not found for public key."));

        var newSliceCommand = new VerifySliceCommand
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = endpoint.Id,
            DepositEndpointPosition = (int)request.WalletDepositEndpointPosition,
            Registry = request.CertificateId.Registry,
            CertificateId = Guid.Parse(request.CertificateId.StreamId.Value),
            Quantity = request.Quantity,
            RandomR = request.RandomR.ToByteArray()
        };

        await _bus.Publish(newSliceCommand);

        return new ReceiveResponse();
    }
}
