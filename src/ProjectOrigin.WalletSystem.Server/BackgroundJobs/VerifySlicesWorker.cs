using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.BackgroundJobs;

public class VerifySlicesWorker : BackgroundService
{
    private readonly ILogger<VerifySlicesWorker> _logger;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly VerifySlicesWorkerOptions _options;

    public VerifySlicesWorker(ILogger<VerifySlicesWorker> logger, IUnitOfWorkFactory unitOfWorkFactory, IOptions<VerifySlicesWorkerOptions> options)
    {
        _logger = logger;
        _unitOfWorkFactory = unitOfWorkFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"{nameof(VerifySlicesWorker)} is working.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoWork(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError("VerifySlicesWorker failed. Error: {ex}", ex);
            }
        }
    }

    private async Task DoWork(CancellationToken stoppingToken)
    {
        using var unitOfWork = _unitOfWorkFactory.Create();

        var receivedSlice = await unitOfWork.CertificateRepository.GetTop1ReceivedSlice();

        if (receivedSlice == null)
        {
            _logger.LogInformation("No received slices found.");
            await Task.Delay(_options.SleepTime, stoppingToken);
            return;
        }

        var registry = await unitOfWork.RegistryRepository.GetRegistryFromName(receivedSlice.Registry);

        if (registry == null)
        {
            _logger.LogError($"Registry with name {receivedSlice.Registry} not found. Deleting received slice from certificate with certificate id {receivedSlice.CertificateId}.");
            await unitOfWork.CertificateRepository.RemoveReceivedSlice(receivedSlice);
            unitOfWork.Commit();
            return;
        }

        //Verify with project origin registry

        try
        {
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
                certificate = new Certificate(slice.CertificateId, registry.Id);
                await unitOfWork.CertificateRepository.InsertCertificate(certificate);
            }

            await unitOfWork.CertificateRepository.InsertSlice(slice);

            await unitOfWork.CertificateRepository.RemoveReceivedSlice(receivedSlice);

            unitOfWork.Commit();
        }
        catch (Exception ex)
        {
            _logger.LogError("Unable to convert ReceivedSlice to Slice. Error: {0}", ex);
            unitOfWork.Rollback();
        }
    }
}
