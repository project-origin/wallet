using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Projections;
using ProjectOrigin.WalletSystem.Server.Services;

namespace ProjectOrigin.WalletSystem.Server.BackgroundJobs;

public class VerifySlicesWorker : BackgroundService
{
    private readonly ILogger<VerifySlicesWorker> _logger;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly VerifySlicesWorkerOptions _options;
    private readonly IRegistryService _registryService;

    public VerifySlicesWorker(ILogger<VerifySlicesWorker> logger, IUnitOfWorkFactory unitOfWorkFactory, IOptions<VerifySlicesWorkerOptions> options, IRegistryService registryService)
    {
        _logger = logger;
        _unitOfWorkFactory = unitOfWorkFactory;
        _options = options.Value;
        _registryService = registryService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"{nameof(VerifySlicesWorker)} is working.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoWorkWithDelay(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError("VerifySlicesWorker failed. Error: {ex}", ex);
            }
        }
    }

    private async Task DoWorkWithDelay(CancellationToken stoppingToken)
    {
        using var unitOfWork = _unitOfWorkFactory.Create();

        var receivedSlice = await unitOfWork.CertificateRepository.GetTop1ReceivedSlice();

        if (receivedSlice == null)
        {
            _logger.LogInformation("No received slices found.");
            await Task.Delay(_options.SleepTime, stoppingToken);
            return;
        }

        await ProcessReceivedSlice(receivedSlice, unitOfWork);
    }

    private async Task ProcessReceivedSlice(ReceivedSlice receivedSlice, UnitOfWork unitOfWork)
    {
        // Get cert from registry
        var certificateProjection = await _registryService.GetGranularCertificate(receivedSlice.Registry, receivedSlice.CertificateId);
        if (certificateProjection is null)
        {
            _logger.LogError($"GranularCertificate with id {receivedSlice.CertificateId} not found in registry {receivedSlice.Registry}. Deleting received slice.");
            await unitOfWork.CertificateRepository.RemoveReceivedSlice(receivedSlice);
            unitOfWork.Commit();
            return;
        }

        // Verify slice
        var secretCommitmentInfo = new PedersenCommitment.SecretCommitmentInfo((uint)receivedSlice.Quantity, receivedSlice.RandomR);
        var sliceId = ByteString.CopyFrom(SHA256.HashData(secretCommitmentInfo.Commitment.C));
        var foundSlice = certificateProjection.GetCertificateSlice(sliceId);
        if (foundSlice is null)
        {
            _logger.LogError($"Slice with id {sliceId} not found in certificate {receivedSlice.CertificateId}. Deleting received slice.");
            await unitOfWork.CertificateRepository.RemoveReceivedSlice(receivedSlice);
            unitOfWork.Commit();
            return;
        }

        await InsertIntoWallet(unitOfWork, receivedSlice, certificateProjection);
    }

    private async Task InsertIntoWallet(UnitOfWork unitOfWork, ReceivedSlice receivedSlice, GranularCertificate certificateProjection)
    {
        try
        {
            var registry = await unitOfWork.RegistryRepository.GetRegistryFromName(receivedSlice.Registry);
            if (registry is null)
            {
                registry = new RegistryModel(Guid.NewGuid(), receivedSlice.Registry);
                await unitOfWork.RegistryRepository.InsertRegistry(registry);
            }

            var slice = new Slice(Guid.NewGuid(),
                receivedSlice.WalletSectionId,
                receivedSlice.WalletSectionPosition,
                registry.Id,
                receivedSlice.CertificateId,
                receivedSlice.Quantity,
                receivedSlice.RandomR);

            var certificate = await unitOfWork.CertificateRepository.GetCertificate(registry.Id, slice.CertificateId);
            if (certificate == null)
            {
                var attributes = certificateProjection.Attributes
                    .Select(a => new CertificateAttribute(a.Key, a.Value))
                    .ToList();

                certificate = new Certificate(slice.CertificateId,
                    registry.Id,
                    certificateProjection.Period.Start.ToDateTimeOffset(),
                    certificateProjection.Period.End.ToDateTimeOffset(),
                    certificateProjection.GridArea,
                    (GranularCertificateType)certificateProjection.Type,
                    attributes);
                await unitOfWork.CertificateRepository.InsertCertificate(certificate);
            }

            await unitOfWork.CertificateRepository.InsertSlice(slice);
            await unitOfWork.CertificateRepository.RemoveReceivedSlice(receivedSlice);

            unitOfWork.Commit();

            _logger.LogInformation($"Slice on certificate ”{slice.CertificateId}” inserted into wallet.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Unable to convert ReceivedSlice to Slice. Error: {0}", ex);
            unitOfWork.Rollback();
        }
    }
}
