using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using ProjectOrigin.WalletSystem.Server.CommandHandlers;
using Quartz;

namespace ProjectOrigin.WalletSystem.Server.Jobs;

public class PublishCheckForWithdrawnCertificatesCommandJob : IJob
{
    private readonly IBus _bus;
    private readonly ILogger<PublishCheckForWithdrawnCertificatesCommandJob> _logger;

    public PublishCheckForWithdrawnCertificatesCommandJob(IBus bus, ILogger<PublishCheckForWithdrawnCertificatesCommandJob> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var message = new CheckForWithdrawnCertificatesCommand { };

        await _bus.Publish(message);

        _logger.LogInformation($"CheckForWithdrawnCertificatesCommand published at: {DateTime.Now}");
    }
}
