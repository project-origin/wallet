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
        var interval = TimeSpan.FromHours(
            _options.IntervalHours > 0 ? _options.IntervalHours : 24);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();

                var cutoff = timeProvider.GetUtcNow().AddDays(-Math.Max(_options.RetentionDays, 1));

                var deleted = await repo.DeleteDisabledWalletsAsync(cutoff);

                logger.LogInformation(
                    "GDPR wallet‑cleanup removed {Count} wallets (disabled before {Cutoff:yyyy‑MM‑dd}).",
                    deleted, cutoff);
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
