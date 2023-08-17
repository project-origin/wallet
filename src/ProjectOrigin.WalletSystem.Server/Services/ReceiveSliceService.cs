using System;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.V1;

namespace ProjectOrigin.WalletSystem.Server.Services;

[AllowAnonymous]
public class ReceiveSliceService : ProjectOrigin.WalletSystem.V1.ReceiveSliceService.ReceiveSliceServiceBase
{
    private readonly ILogger<ReceiveSliceService> _logger;
    private readonly UnitOfWork _unitOfWork;
    private readonly IHDAlgorithm _hdAlgorithm;

    public ReceiveSliceService(ILogger<ReceiveSliceService> logger, UnitOfWork unitOfWork, IHDAlgorithm hdAlgorithm)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _hdAlgorithm = hdAlgorithm;
    }

    public override async Task<ReceiveResponse> ReceiveSlice(ReceiveRequest request, ServerCallContext context)
    {
        var publicKey = _hdAlgorithm.ImportHDPublicKey(request.WalletDepositEndpointPublicKey.Span);
        var depositEndpoint = await _unitOfWork.WalletRepository.GetDepositEndpointFromPublicKey(publicKey);

        if (depositEndpoint == null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "DepositEndpoint not found for public key."));

        var newSlice = new ReceivedSlice(Guid.NewGuid(),
            depositEndpoint.Id,
            (int)request.WalletDepositEndpointPosition,
            request.CertificateId.Registry,
            Guid.Parse(request.CertificateId.StreamId.Value),
            request.Quantity,
            request.RandomR.ToByteArray());

        await _unitOfWork.CertificateRepository.InsertReceivedSlice(newSlice);

        _unitOfWork.Commit();

        return new ReceiveResponse();
    }
}
