using System;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ProjectOrigin.Register.V1;
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
        var walletSection = await GetWalletSectionFromPublicKey(publicKey);

        var (registryId, certificateId) = await GetOrInsertCertificate(request.CertificateId);

        var newSlice = new Slice(Guid.NewGuid(),
                                 walletSection.Id,
                                 (int)request.WalletSectionPosition,
                                 registryId,
                                 certificateId,
                                 request.Quantity,
                                 request.RandomR.ToByteArray(),
                                 SliceState.Unverified);

        await _unitOfWork.CertificateRepository.InsertSlice(newSlice);

        _unitOfWork.Commit();

        return new ReceiveResponse();
    }

    private async Task<WalletSection> GetWalletSectionFromPublicKey(IHDPublicKey publicKey)
    {
        var walletSection = await _unitOfWork.WalletRepository.GetWalletSectionFromPublicKey(publicKey);

        if (walletSection == null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "WalletSection not found for public key."));

        return walletSection;
    }

    private async Task<(Guid RegistryId, Guid CertificateId)> GetOrInsertCertificate(FederatedStreamId federatedStreamId)
    {
        var certId = Guid.Parse(federatedStreamId.StreamId.Value);
        var registry = await _unitOfWork.RegistryRepository.GetRegistryFromName(federatedStreamId.Registry);

        if (registry == null)
        {
            registry = new Registry(Guid.NewGuid(), federatedStreamId.Registry);
            await _unitOfWork.RegistryRepository.InsertRegistry(registry);
        }

        var certificate = await _unitOfWork.CertificateRepository.GetCertificate(registry.Id, certId);
        if (certificate == null)
        {
            certificate = new Certificate(certId, registry.Id, CertificateState.Inserted);
            await _unitOfWork.CertificateRepository.InsertCertificate(certificate);
        }

        return (registry.Id, certificate.Id);
    }
}
