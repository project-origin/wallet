using System;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.HDWallet;
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
        var publicKey = _hdAlgorithm.ImportPublicKey(request.WalletSectionPublicKey.Span);
        var walletSection = await _unitOfWork.WalletRepository.GetWalletSectionFromPublicKey(publicKey);

        if (walletSection == null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "WalletSection not found for public key."));

        var newSlice = new ReceivedSlice(Guid.NewGuid(),
                                 walletSection.Id,
                                 (int)request.WalletSectionPosition,
                                 request.CertificateId.Registry,
                                 Guid.Parse(request.CertificateId.StreamId.Value),
                                 request.Quantity,
                                 request.RandomR.ToByteArray());

        await _unitOfWork.CertificateRepository.InsertReceivedSlice(newSlice);

        _unitOfWork.Commit();

        return new ReceiveResponse();
    }
}
