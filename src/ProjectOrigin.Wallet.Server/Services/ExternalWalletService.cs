using System;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProjectOrigin.Wallet.Server.Database;
using ProjectOrigin.Wallet.V1;

namespace ProjectOrigin.Wallet.Server.Services;

internal class ExternalWalletService : ProjectOrigin.Wallet.V1.ExternalWalletService.ExternalWalletServiceBase
{
    private readonly ILogger<ExternalWalletService> _logger;
    private readonly UnitOfWork _unitOfWork;

    public ExternalWalletService(ILogger<ExternalWalletService> logger, UnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public override async Task<ReceiveResponse> ReceiveSlice(ReceiveRequest request, ServerCallContext context)
    {
        //TODO get registry
        var subject = context.GetSubject();
        var wallet = await _unitOfWork.WalletRepository.GetWallet(subject);

        if (wallet == null)
            throw new ArgumentException("You don't have a wallet. Create a wallet before receiving slices.");
        
        var walletSection = await _unitOfWork.WalletRepository.GetWalletSection(wallet.Id, request.WalletSectionPosition);

        //TODO purpose of request.WalletSectionPublicKey?

        return new ReceiveResponse();
    }
}
