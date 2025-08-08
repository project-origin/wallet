using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Options;

namespace ProjectOrigin.Vault.Jobs;

public sealed class WalletCleanupJob : OnlyRunOncePrReplicaJobBase
{
    private readonly WalletCleanupOptions _options;

    public WalletCleanupJob(
        IOptions<WalletCleanupOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<WalletCleanupJob> logger)
        : base(
            jobName: nameof(WalletCleanupJob),
            jobKey: JobKeys.WalletCleanupJob,
            runIntervalInSeconds: Math.Max(1,
                (options.Value.IntervalHours > 0 ? options.Value.IntervalHours : 24) * 3600),
            scopeFactory: scopeFactory,
            logger: logger,
            enabled: options.Value.Enabled)
    {
        _options = options.Value;
    }

    protected override async Task PerformPeriodicTask(IServiceScope scope, CancellationToken stoppingToken)
    {
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var cutoff = DateTimeOffset.UtcNow.AddDays(-_options.RetentionDays);

        var (_, deletedWallets) = await uow.WalletRepository.DeleteDisabledWalletsAsync(cutoff);

        if (_options.LogDeletedWalletDetails)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<WalletCleanupJob>>();
            foreach (var (walletId, owner, _) in deletedWallets)
                logger.LogInformation("Deleted wallet {WalletId} owned by {Owner}", walletId, owner);
        }
    }
}
