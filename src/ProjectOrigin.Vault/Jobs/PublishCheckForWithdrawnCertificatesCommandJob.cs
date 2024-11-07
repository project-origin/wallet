using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Vault.CommandHandlers;
using ProjectOrigin.Vault.Options;

namespace ProjectOrigin.Vault.Jobs;

public class PublishCheckForWithdrawnCertificatesCommandJob : OnlyRunOncePrReplicaJobBase
{
    private readonly IBus _bus;

    public PublishCheckForWithdrawnCertificatesCommandJob(IBus bus, IOptions<JobOptions> options, ILogger<PublishCheckForWithdrawnCertificatesCommandJob> logger, IServiceScopeFactory scopeFactory)
        : base(nameof(PublishCheckForWithdrawnCertificatesCommandJob),
            JobKeys.PublishCheckForWithdrawnCertificatesCommandJob,
            options.Value.CheckForWithdrawnCertificatesIntervalInSeconds,
            scopeFactory,
            logger)
    {
        _bus = bus;
    }

    protected override async Task PerformPeriodicTask(IServiceScope scope, CancellationToken stoppingToken)
    {
        var message = new CheckForWithdrawnCertificatesCommand { };
        await _bus.Publish(message, stoppingToken);
    }
}
