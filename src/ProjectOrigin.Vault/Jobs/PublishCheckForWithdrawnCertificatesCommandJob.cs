using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Vault.CommandHandlers;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Options;

namespace ProjectOrigin.Vault.Jobs;

public class PublishCheckForWithdrawnCertificatesCommandJob : BackgroundService
{
    private readonly IBus _bus;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PublishCheckForWithdrawnCertificatesCommandJob> _logger;
    private readonly JobOptions _options;

    private const int LockKey = (int)JobKeys.PublishCheckForWithdrawnCertificatesCommandJob;
    private const string JobName = nameof(PublishCheckForWithdrawnCertificatesCommandJob);

    public PublishCheckForWithdrawnCertificatesCommandJob(IBus bus, IUnitOfWork unitOfWork, IOptions<JobOptions> options, ILogger<PublishCheckForWithdrawnCertificatesCommandJob> logger)
    {
        _bus = bus;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool acquiredLock = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!acquiredLock)
            {
                acquiredLock = await _unitOfWork.JobExecutionRepository.AcquireAdvisoryLock(LockKey);
            }

            if (acquiredLock)
            {
                try
                {
                    if (await HasBeenRunByOtherReplica())
                    {
                        var willRunAt = DateTimeOffset.UtcNow.AddSeconds(_options.CheckForWithdrawnCertificatesIntervalInSeconds);
                        _logger.LogInformation("PublishCheckForWithdrawnCertificatesCommandJob was executed at {now} but did not publish. Will run again at {willRunAt}", DateTime.Now, willRunAt);
                        await _unitOfWork.JobExecutionRepository.ReleaseAdvisoryLock(LockKey);
                        await Sleep(stoppingToken);
                        continue;
                    }

                    await PerformPeriodicPublish(stoppingToken);

                    await _unitOfWork.JobExecutionRepository.UpdateLastExecutionTime(JobName, DateTimeOffset.UtcNow);
                    _unitOfWork.Commit();

                    await _unitOfWork.JobExecutionRepository.ReleaseAdvisoryLock(LockKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while executing {JobName}", JobName);
                    _unitOfWork.Rollback();
                    await _unitOfWork.JobExecutionRepository.ReleaseAdvisoryLock(LockKey);
                }
            }

            await Sleep(stoppingToken);
        }

        if (acquiredLock)
        {
            await _unitOfWork.JobExecutionRepository.ReleaseAdvisoryLock(LockKey);
        }
    }

    private async Task<bool> HasBeenRunByOtherReplica()
    {
        var lastExecutionTime = await _unitOfWork.JobExecutionRepository.GetLastExecutionTime(JobName);
        return lastExecutionTime != null && (DateTimeOffset.UtcNow - lastExecutionTime.Value).TotalSeconds < _options.TimeBeforeItIsOkToRunCheckForWithdrawnCertificatesAgain();
    }

    private async Task PerformPeriodicPublish(CancellationToken stoppingToken)
    {
        var willRunAt = DateTimeOffset.UtcNow.AddSeconds(_options.CheckForWithdrawnCertificatesIntervalInSeconds);
        var message = new CheckForWithdrawnCertificatesCommand { };
        await _bus.Publish(message, stoppingToken);

        _logger.LogInformation("CheckForWithdrawnCertificatesCommand published at: {now}. Will run again at {willRunAt}", DateTime.Now, willRunAt);
    }

    private async Task Sleep(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(_options.CheckForWithdrawnCertificatesIntervalInSeconds), stoppingToken);
    }
}
