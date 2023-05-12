using System;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProjectOrigin.Wallet.Server.Database;
using ProjectOrigin.Wallet.Server.Models;

namespace ProjectOrigin.Wallet.Server.Services;

internal class WalletService : ProjectOrigin.Wallet.V1.WalletService.WalletServiceBase
{
    private readonly ILogger<WalletService> _logger;
    private readonly UnitOfWork _unitOfWork;

    public WalletService(ILogger<WalletService> logger, UnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public override async Task<V1.WalletSectionReference> CreateWalletSection(V1.CreateWalletSectionRequest request, ServerCallContext context)
    {
        await _unitOfWork.WalletRepository.Create(new MyTable(0, Guid.NewGuid().ToString()));
        _unitOfWork.Commit();
        var tables = await _unitOfWork.WalletRepository.GetAll();

        return new V1.WalletSectionReference();
    }
}
