using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.HDWallet;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.V1;

namespace ProjectOrigin.WalletSystem.Server.Services;

[Authorize]
public class WalletService : ProjectOrigin.WalletSystem.V1.WalletService.WalletServiceBase
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

        var wallet = await _unitOfWork.WalletRepository.GetWalletByOwner(subject);

        if (wallet is null)
        {
            var key = _hdAlgorithm.GenerateNewPrivateKey();
            wallet = new Wallet(Guid.NewGuid(), subject, key);
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

    public override async Task<QueryResponse> QueryGranularCertificates(QueryRequest request, ServerCallContext context)
    {
        var subject = context.GetSubject();

        var certificates = await _unitOfWork.CertificateRepository.GetAllOwnedCertificates(subject);

        var response = new QueryResponse();
        foreach (var gc in certificates)
        {
            response.GranularCertificates.Add(gc.ToProto());
        }

        return response;
    }
}
