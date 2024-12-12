using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectOrigin.Vault.Database;

namespace ProjectOrigin.Vault.Jobs;

public abstract class OnlyRunOncePrReplicaJobBase : BackgroundService
{
    private readonly string _jobName;
    private readonly JobKeys _jobKey;
    private readonly int _runIntervalInSeconds;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly bool _enabled;

    protected OnlyRunOncePrReplicaJobBase(string jobName, JobKeys jobKey, int runIntervalInSeconds, IServiceScopeFactory scopeFactory, ILogger logger, bool enabled)
    {
        _jobName = jobName;
        _jobKey = jobKey;
        _runIntervalInSeconds = runIntervalInSeconds;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _enabled = enabled;
    }

    protected abstract Task PerformPeriodicTask(IServiceScope scope, CancellationToken stoppingToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("{jobName} is disabled!", _jobName);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("{jobName} is running at: {time}", _jobName, DateTimeOffset.Now);
                using (var scope = _scopeFactory.CreateScope())
                {
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                    var acquiredLock = await unitOfWork.JobExecutionRepository.AcquireAdvisoryLock((int)_jobKey);

                    if (acquiredLock)
                    {
                        try
                        {
                            _logger.LogInformation("{jobName} acquired lock.", _jobName);
                            if (await HasBeenRunByOtherReplica(unitOfWork))
                            {
                                _logger.LogInformation("{jobName} was executed at {now} but did not publish. Will run again at {willRunAt}", _jobName, DateTime.Now, DateTimeOffset.UtcNow.AddSeconds(_runIntervalInSeconds));
                            }
                            else
                            {
                                await PerformPeriodicTask(scope, stoppingToken);

                                _logger.LogInformation("{jobName} done: {now}. Will run again at {willRunAt}", _jobName, DateTime.Now, DateTimeOffset.UtcNow.AddSeconds(_runIntervalInSeconds));

                                await unitOfWork.JobExecutionRepository.UpdateLastExecutionTime(_jobName, DateTimeOffset.UtcNow);
                                unitOfWork.Commit();
                            }
                        }
                        catch (Exception)
                        {
                            _logger.LogInformation("{jobName} rollback.", _jobName);
                            unitOfWork.Rollback();
                            throw;
                        }
                        finally
                        {
                            _logger.LogInformation("{jobName} trying to release lock.", _jobName);
                            await unitOfWork.JobExecutionRepository.ReleaseAdvisoryLock((int)_jobKey);
                            _logger.LogInformation("{jobName} released lock.", _jobName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while executing {JobName}", _jobName);
            }
            finally
            {
                await Sleep(stoppingToken);
            }
        }
    }

    private async Task<bool> HasBeenRunByOtherReplica(IUnitOfWork unitOfWork)
    {
        var lastExecutionTime = await unitOfWork.JobExecutionRepository.GetLastExecutionTime(_jobName);
        return lastExecutionTime != null && (DateTimeOffset.UtcNow - lastExecutionTime.Value).TotalSeconds < TimeBeforeItIsOkToRunAgain();
    }

    private int TimeBeforeItIsOkToRunAgain()
    {
        return ((_runIntervalInSeconds * 2) / 3);
    }

    private async Task Sleep(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(_runIntervalInSeconds), stoppingToken);
    }
}
