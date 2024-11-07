using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Options;

namespace ProjectOrigin.Vault.Jobs;

public class ExpireCertificatesJob : OnlyRunOncePrReplicaJobBase
{
    private readonly NetworkOptions _networkOptions;

    public ExpireCertificatesJob(IOptions<JobOptions> options,
        IOptions<NetworkOptions> networkOptions,
        ILogger<ExpireCertificatesJob> logger, IServiceScopeFactory scopeFactory)
        : base(nameof(ExpireCertificatesJob),
            JobKeys.ExpireCertificatesJob,
            options.Value.ExpireCertificatesIntervalInSeconds,
            scopeFactory,
            logger)
    {
        _networkOptions = networkOptions.Value;
    }

    protected override async Task PerformPeriodicTask(IServiceScope scope, CancellationToken stoppingToken)
    {
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await unitOfWork.CertificateRepository.ExpireSlices(DateTimeOffset.UtcNow.AddDays(-_networkOptions.DaysBeforeCertificatesExpire));
    }
}
