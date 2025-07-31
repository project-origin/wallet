using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using ProjectOrigin.Vault.Jobs;
using ProjectOrigin.Vault.Options;
using ProjectOrigin.Vault.Repositories;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using ProjectOrigin.Vault.Tests.TestExtensions;
using Xunit;
using ProjectOrigin.Vault.Database;

namespace ProjectOrigin.Vault.Tests.JobTests;

public class WalletCleanupWorkerTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly PostgresDatabaseFixture _dbFixture;

    public WalletCleanupWorkerTests(PostgresDatabaseFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    [Fact]
    public async Task GivenWalletsWithDifferentDisabledDates_WhenWorkerRuns_ThenDeletesOldWalletsAndKeepsRecentOnes()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var retentionDays = 365;
        var intervalHours = 1;
        var testId = Guid.NewGuid().ToString();

        var opts = Microsoft.Extensions.Options.Options.Create(new WalletCleanupOptions
        {
            Enabled = true,
            IntervalHours = intervalHours,
            RetentionDays = retentionDays,
            LogDeletedWalletDetails = false
        });

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(s =>
            {
                s.AddSingleton(new LoggerFactory().CreateLogger<WalletCleanupWorker>());
                s.AddSingleton(_dbFixture.GetConnectionFactory());
                s.AddSingleton<TimeProvider>(fakeTime);
                s.AddSingleton(opts);

                s.AddSingleton<IWalletRepository>(provider =>
                {
                    var connectionFactory = provider.GetRequiredService<IDbConnectionFactory>();
                    var connection = connectionFactory.CreateConnection();
                    connection.Open();
                    return new WalletRepository(connection);
                });

                s.AddHostedService<WalletCleanupWorker>();
            })
            .Build();

        await host.StartAsync();

        var oldWallet = await _dbFixture.CreateWallet($"old-owner-{testId}");
        var recentWallet = await _dbFixture.CreateWallet($"recent-owner-{testId}");

        using (var connection = _dbFixture.GetConnectionFactory().CreateConnection())
        {
            connection.Open();
            var walletRepository = new WalletRepository(connection);

            await walletRepository.DisableWallet(oldWallet.Id, fakeTime.GetUtcNow().AddDays(-(retentionDays + 1)));

            await walletRepository.DisableWallet(recentWallet.Id, fakeTime.GetUtcNow().AddDays(-(retentionDays - 1)));
        }

        fakeTime.Advance(TimeSpan.FromHours(intervalHours + 1));

        await Task.Delay(TimeSpan.FromSeconds(2));

        using var innerConnection = _dbFixture.GetConnectionFactory().CreateConnection();
        innerConnection.Open();
        var innerWalletRepository = new WalletRepository(innerConnection);

        var oldWalletAfterCleanup = await innerWalletRepository.GetWallet(oldWallet.Id);
        var recentWalletAfterCleanup = await innerWalletRepository.GetWallet(recentWallet.Id);

        oldWalletAfterCleanup.Should().BeNull("Old wallet should be deleted");
        recentWalletAfterCleanup.Should().NotBeNull("Recent wallet should still exist");

        await host.StopAsync();
    }

    [Fact]
    public async Task GivenDisabledWallets_WhenDeleteDisabledWalletsAsyncCalled_ThenReturnsCorrectInformation()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var cutoff = fakeTime.GetUtcNow().AddDays(-365);
        var testId = Guid.NewGuid().ToString();

        var wallet = await _dbFixture.CreateWallet($"test-owner-{testId}");
        using (var connection = _dbFixture.GetConnectionFactory().CreateConnection())
        {
            connection.Open();
            var walletRepository = new WalletRepository(connection);
            await walletRepository.DisableWallet(wallet.Id, fakeTime.GetUtcNow().AddDays(-366)); // Disabled 366 days ago
        }

        using (var innerConnection = _dbFixture.GetConnectionFactory().CreateConnection())
        {
            innerConnection.Open();
            var innerWalletRepository = new WalletRepository(innerConnection);
            var (count, deletedWallets) = await innerWalletRepository.DeleteDisabledWalletsAsync(cutoff);

            count.Should().Be(1);
            deletedWallets.Should().HaveCount(1);
            deletedWallets[0].Id.Should().Be(wallet.Id);
            deletedWallets[0].Owner.Should().Be($"test-owner-{testId}");
            deletedWallets[0].DisabledDate.Should().BeCloseTo(fakeTime.GetUtcNow().AddDays(-366), TimeSpan.FromSeconds(1));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GivenLogDeletedWalletDetailsOption_WhenWorkerRuns_ThenRespectsLogDetailsSetting(bool logDetails)
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var intervalHours = 1;
        var testId = Guid.NewGuid().ToString();

        var opts = Microsoft.Extensions.Options.Options.Create(new WalletCleanupOptions
        {
            Enabled = true,
            IntervalHours = intervalHours,
            RetentionDays = 365,
            LogDeletedWalletDetails = logDetails
        });

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(s =>
            {
                s.AddSingleton(new LoggerFactory().CreateLogger<WalletCleanupWorker>());
                s.AddSingleton(_dbFixture.GetConnectionFactory());
                s.AddSingleton<TimeProvider>(fakeTime);
                s.AddSingleton(opts);

                s.AddSingleton<IWalletRepository>(provider =>
                {
                    var connectionFactory = provider.GetRequiredService<IDbConnectionFactory>();
                    var connection = connectionFactory.CreateConnection();
                    connection.Open();
                    return new WalletRepository(connection);
                });

                s.AddHostedService<WalletCleanupWorker>();
            })
            .Build();

        await host.StartAsync();

        var wallet = await _dbFixture.CreateWallet($"test-owner-{testId}");

        using (var connection = _dbFixture.GetConnectionFactory().CreateConnection())
        {
            connection.Open();
            var walletRepository = new WalletRepository(connection);
            await walletRepository.DisableWallet(wallet.Id, fakeTime.GetUtcNow().AddDays(-366));
        }

        fakeTime.Advance(TimeSpan.FromHours(intervalHours + 1));
        await Task.Delay(TimeSpan.FromSeconds(2));

        using var innerConnection = _dbFixture.GetConnectionFactory().CreateConnection();
        innerConnection.Open();
        var innerWalletRepository = new WalletRepository(innerConnection);
        var walletAfterCleanup = await innerWalletRepository.GetWallet(wallet.Id);
        walletAfterCleanup.Should().BeNull("Wallet should be deleted");

        await host.StopAsync();
    }

    [Fact]
    public async Task GivenWorkerWithDefaultOptions_WhenWorkerRuns_ThenDeletionIsDisabled()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var testId = Guid.NewGuid().ToString();

        var opts = Microsoft.Extensions.Options.Options.Create(new WalletCleanupOptions
        {
            IntervalHours = 1,
            RetentionDays = 365,
            LogDeletedWalletDetails = false
        });

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(s =>
            {
                s.AddSingleton(new LoggerFactory().CreateLogger<WalletCleanupWorker>());
                s.AddSingleton(_dbFixture.GetConnectionFactory());
                s.AddSingleton<TimeProvider>(fakeTime);
                s.AddSingleton(opts);

                s.AddSingleton<IWalletRepository>(provider =>
                {
                    var connectionFactory = provider.GetRequiredService<IDbConnectionFactory>();
                    var connection = connectionFactory.CreateConnection();
                    connection.Open();
                    return new WalletRepository(connection);
                });

                s.AddHostedService<WalletCleanupWorker>();
            })
            .Build();

        await host.StartAsync();

        var wallet = await _dbFixture.CreateWallet($"test-owner-{testId}");

        using (var connection = _dbFixture.GetConnectionFactory().CreateConnection())
        {
            connection.Open();
            var walletRepository = new WalletRepository(connection);
            await walletRepository.DisableWallet(wallet.Id, fakeTime.GetUtcNow().AddDays(-366));
        }

        fakeTime.Advance(TimeSpan.FromHours(2));
        await Task.Delay(TimeSpan.FromSeconds(2));

        using var innerConnection = _dbFixture.GetConnectionFactory().CreateConnection();
        innerConnection.Open();
        var innerWalletRepository = new WalletRepository(innerConnection);
        var walletAfterCleanup = await innerWalletRepository.GetWallet(wallet.Id);
        walletAfterCleanup.Should().NotBeNull("Wallet should NOT be deleted when worker is disabled");

        await host.StopAsync();
    }
}
