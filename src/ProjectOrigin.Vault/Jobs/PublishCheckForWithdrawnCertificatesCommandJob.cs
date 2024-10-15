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

    public PublishCheckForWithdrawnCertificatesCommandJob(IBus bus, IUnitOfWork unitOfWork, IOptions<JobOptions> options, ILogger<PublishCheckForWithdrawnCertificatesCommandJob> logger)
    {
        _bus = bus;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var willRunAt = DateTimeOffset.UtcNow.AddSeconds(_options.CheckForWithdrawnCertificatesIntervalInSeconds);
            var lastExecutionTime = await _unitOfWork.JobExecutionRepository.GetLastExecutionTime(nameof(PublishCheckForWithdrawnCertificatesCommandJob));

            if (lastExecutionTime != null && (DateTimeOffset.UtcNow - lastExecutionTime.Value).TotalSeconds < _options.TimeBeforeItIsOkToRunCheckForWithdrawnCertificatesAgain())
            {
                _logger.LogInformation("PublishCheckForWithdrawnCertificatesCommandJob was executed at {now} but did not publish. Will run again at {willRunAt}", DateTime.Now, willRunAt);
                return;
            }

            await _unitOfWork.JobExecutionRepository.UpdateLastExecutionTime(nameof(PublishCheckForWithdrawnCertificatesCommandJob), DateTimeOffset.UtcNow);
            _unitOfWork.Commit();

            var message = new CheckForWithdrawnCertificatesCommand { };
            await _bus.Publish(message, stoppingToken);

            _logger.LogInformation("CheckForWithdrawnCertificatesCommand published at: {now}. Will run again at {willRunAt}", DateTime.Now, willRunAt);

            await Task.Delay(TimeSpan.FromSeconds(_options.CheckForWithdrawnCertificatesIntervalInSeconds), stoppingToken);
        }
    }
}
