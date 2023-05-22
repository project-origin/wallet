using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Wallet.Server.Database;
using ProjectOrigin.Wallet.Server.HDWallet;
using ProjectOrigin.Wallet.Server.Models;
using ProjectOrigin.Wallet.V1;

namespace ProjectOrigin.Wallet.Server.Services;

[Authorize]
public class WalletService : ProjectOrigin.Wallet.V1.WalletService.WalletServiceBase
{
    private readonly string _endpointAddress;
    private readonly ILogger<WalletService> _logger;
    private readonly UnitOfWork _unitOfWork;
    private readonly IHDAlgorithm _hdAlgorithm;

    public WalletService(ILogger<WalletService> logger, UnitOfWork unitOfWork, IHDAlgorithm hdAlgorithm, IOptions<ServiceOptions> options)
    {
        _endpointAddress = options.Value.EndpointAddress;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _hdAlgorithm = hdAlgorithm;
    }

    public override async Task<V1.WalletSectionReference> CreateWalletSection(V1.CreateWalletSectionRequest request, ServerCallContext context)
    {
        var subject = context.GetSubject();

        var wallet = await _unitOfWork.WalletRepository.GetWalletFromOwner(subject);

        if (wallet is null)
        {
            var key = _hdAlgorithm.GenerateNewPrivateKey();
            wallet = new OwnerWallet(Guid.NewGuid(), subject, key);
            await _unitOfWork.WalletRepository.Create(wallet);
        }

        int nextPosition = await _unitOfWork.WalletRepository.GetNextWalletPosition(wallet.Id);

        var section = new WalletSection(Guid.NewGuid(), wallet.Id, nextPosition, wallet.PrivateKey.Derive(nextPosition).PublicKey);
        await _unitOfWork.WalletRepository.CreateSection(section);
        _unitOfWork.Commit();

        return new V1.WalletSectionReference()
        {
            Version = 1,
            Endpoint = _endpointAddress,
            SectionPublicKey = ByteString.CopyFrom(section.PublicKey.Export())
        };
    }

    public override Task<QueryResponse> QueryGranularCertificates(QueryRequest request, ServerCallContext context)
    {
        var subject = context.GetSubject();

    }
}
