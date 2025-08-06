using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Vault.Options;
using ProjectOrigin.Vault.Repositories;

namespace ProjectOrigin.Vault.Jobs;

public class WalletCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WalletCleanupOptions> options,
    ILogger<WalletCleanupWorker> logger,
    TimeProvider timeProvider)
    : BackgroundService
{
    private readonly WalletCleanupOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Wallet cleanup worker is disabled");
            return;
        }

        var interval = TimeSpan.FromHours(
            _options.IntervalHours > 0 ? _options.IntervalHours : 24);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();

                var cutoff = timeProvider.GetUtcNow().AddDays(-_options.RetentionDays);

                var (_, deletedWallets) = await repo.DeleteDisabledWalletsAsync(cutoff);

                logger.LogInformation("Wallet cleanup worker completed successfully");

                if (_options.LogDeletedWalletDetails)
                {
                    foreach (var (walletId, owner, _) in deletedWallets)
                    {
                        logger.LogInformation(
                            "Deleted wallet {WalletId} owned by {Owner}",
                            walletId, owner);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Wallet‑cleanup job failed");
            }

            try
            {
                await Task.Delay(interval, timeProvider, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}
