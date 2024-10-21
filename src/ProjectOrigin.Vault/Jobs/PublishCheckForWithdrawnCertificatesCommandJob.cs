using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly ILogger<PublishCheckForWithdrawnCertificatesCommandJob> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JobOptions _options;

    private const int LockKey = (int)JobKeys.PublishCheckForWithdrawnCertificatesCommandJob;
    private const string JobName = nameof(PublishCheckForWithdrawnCertificatesCommandJob);

    public PublishCheckForWithdrawnCertificatesCommandJob(IBus bus, IOptions<JobOptions> options, ILogger<PublishCheckForWithdrawnCertificatesCommandJob> logger, IServiceScopeFactory scopeFactory)
    {
        _bus = bus;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var acquiredLock = false;

            using (var scope = _scopeFactory.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                try
                {
                    acquiredLock = await unitOfWork.JobExecutionRepository.AcquireAdvisoryLock(LockKey);

                    if (acquiredLock)
                    {
                        if (await HasBeenRunByOtherReplica(unitOfWork))
                        {
                            var willRunAt =
                                DateTimeOffset.UtcNow.AddSeconds(_options.CheckForWithdrawnCertificatesIntervalInSeconds);
                            _logger.LogInformation(
                                "PublishCheckForWithdrawnCertificatesCommandJob was executed at {now} but did not publish. Will run again at {willRunAt}",
                                DateTime.Now, willRunAt);
                            continue;
                        }

                        await PerformPeriodicPublish(stoppingToken);

                        await unitOfWork.JobExecutionRepository.UpdateLastExecutionTime(JobName, DateTimeOffset.UtcNow);
                        unitOfWork.Commit();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while executing {JobName}", JobName);
                    unitOfWork.Rollback();
                }
                finally
                {
                    if (acquiredLock)
                    {
                        await unitOfWork.JobExecutionRepository.ReleaseAdvisoryLock(LockKey);
                    }

                    await Sleep(stoppingToken);
                }

            }

        }
    }

    private async Task<bool> HasBeenRunByOtherReplica(IUnitOfWork unitOfWork)
    {
        var lastExecutionTime = await unitOfWork.JobExecutionRepository.GetLastExecutionTime(JobName);
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
