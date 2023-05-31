using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.BackgroundJobs;

public class VerifySlicesWorker : BackgroundService
{
    private readonly UnitOfWork _unitOfWork;
    private readonly ILogger<VerifySlicesWorker> _logger;

    public VerifySlicesWorker(UnitOfWork unitOfWork, ILogger<VerifySlicesWorker> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DoWork(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task DoWork(CancellationToken stoppingToken)
    {
        var repository = _unitOfWork.CertificateRepository;

        var receivedSlice = await repository.GetTop1ReceivedSlice();

        if (receivedSlice == null)
        {
            _logger.LogInformation("No received slices found.");
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            return;
        }

        var registry = await _unitOfWork.RegistryRepository.GetRegistryFromName(receivedSlice.Registry);

        if (registry == null)
        {
            _logger.LogError($"Registry with name {0} not found.", receivedSlice.Registry);
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

            await _unitOfWork.CertificateRepository.InsertSlice(slice);

            await _unitOfWork.CertificateRepository.RemoveReceivedSlice(receivedSlice);

            _unitOfWork.Commit();
        }
        catch (Exception ex)
        {
            _logger.LogError("Unable to verify received slice against project origin. Error: {0} ", ex);
            _unitOfWork.Rollback();
        }
    }
}
