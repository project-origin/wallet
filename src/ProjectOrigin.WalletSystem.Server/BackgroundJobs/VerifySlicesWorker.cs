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
        var unitOfWork = _unitOfWorkFactory.Create();

        var receivedSlice = await unitOfWork.CertificateRepository.GetTop1ReceivedSlice();

        if (receivedSlice == null)
        {
            _logger.LogInformation("No received slices found.");
            await Task.Delay(TimeSpan.FromSeconds(_options.SleepTimeInSeconds), stoppingToken);
            return;
        }

        var registry = await unitOfWork.RegistryRepository.GetRegistryFromName(receivedSlice.Registry);

        if (registry == null)
        {
            _logger.LogError($"Registry with name {0} not found. Deleting received slice from certificate with certificate id {1}.", receivedSlice.Registry, receivedSlice.CertificateId);
            await unitOfWork.CertificateRepository.RemoveReceivedSlice(receivedSlice);
            return;
        }

        try
        {
            var slice = new Slice(Guid.NewGuid(),
                receivedSlice.WalletSectionId,
                receivedSlice.WalletSectionPosition,
                registry.Id,
                receivedSlice.CertificateId,
                receivedSlice.Quantity,
                receivedSlice.RandomR);

            //Verify with project origin registry

            await unitOfWork.CertificateRepository.InsertSlice(slice);

            await unitOfWork.CertificateRepository.RemoveReceivedSlice(receivedSlice);

            unitOfWork.Commit();
        }
        catch (Exception ex)
        {
            _logger.LogError("Unable to verify received slice against project origin. Deleting received slice from certificate with certificate id {0}. Error: {1}", receivedSlice.CertificateId, ex);
            unitOfWork.Rollback();
            await unitOfWork.CertificateRepository.RemoveReceivedSlice(receivedSlice);
        }
    }
}
