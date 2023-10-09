using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.Server.CommandHandlers;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;
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
        var endpoint = await _unitOfWork.WalletRepository.GetWalletEndpoint(publicKey);

        if (endpoint == null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "DepositEndpoint not found for public key."));

        var newSliceCommand = new VerifySliceCommand
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = (int)request.WalletDepositEndpointPosition,
            Registry = request.CertificateId.Registry,
            CertificateId = Guid.Parse(request.CertificateId.StreamId.Value),
            Quantity = request.Quantity,
            RandomR = request.RandomR.ToByteArray(),
            HashedAttributes = request.HashedAttributes.Select(x => new WalletAttribute
            {
                WalletId = endpoint.WalletId,
                CertificateId = Guid.Parse(request.CertificateId.StreamId.Value),
                RegistryName = request.CertificateId.Registry,
                Key = x.Key,
                Value = x.Value,
                Salt = x.Salt.ToByteArray()
            }).ToList()
        };

        await _bus.Publish(newSliceCommand);

        return new ReceiveResponse();
    }
}
