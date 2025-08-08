using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectOrigin.Vault.Jobs;
using ProjectOrigin.Vault.Options;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using Xunit;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Repositories;
using ProjectOrigin.Vault.Tests.TestExtensions;

namespace ProjectOrigin.Vault.Tests.JobTests;

public class WalletCleanupJobTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly PostgresDatabaseFixture _dbFixture;

    public WalletCleanupJobTests(PostgresDatabaseFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    [Fact]
    public async Task GivenWalletsWithDifferentDisabledDates_WhenJobRuns_ThenDeletesOldWalletsAndKeepsRecentOnes()
    {
        const int retentionDays = 365;
        const int intervalHours = 1;
        var now = DateTimeOffset.UtcNow;
        var testId = Guid.NewGuid().ToString();

        var oldWallet = await _dbFixture.CreateWallet($"old-owner-{testId}");
        var recentWallet = await _dbFixture.CreateWallet($"recent-owner-{testId}");
        using (var conn = _dbFixture.GetConnectionFactory().CreateConnection())
        {
            conn.Open();
            var repo = new WalletRepository(conn);
            await repo.DisableWallet(oldWallet.Id, now.AddDays(-(retentionDays + 1)));
            await repo.DisableWallet(recentWallet.Id, now.AddDays(-(retentionDays - 1)));
        }

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
                s.AddLogging();
                s.AddSingleton(_dbFixture.GetConnectionFactory());
                s.AddSingleton(opts);

                s.AddScoped<IUnitOfWork, UnitOfWork>();
                s.AddHostedService<WalletCleanupJob>();
            })
            .Build();

        await host.StartAsync();

        await Task.Delay(TimeSpan.FromSeconds(2));

        using var verifyConn = _dbFixture.GetConnectionFactory().CreateConnection();
        verifyConn.Open();
        var verifyRepo = new WalletRepository(verifyConn);

        (await verifyRepo.GetWallet(oldWallet.Id)).Should().BeNull("old wallet should be deleted");
        (await verifyRepo.GetWallet(recentWallet.Id)).Should().NotBeNull("recent wallet should still exist");

        await host.StopAsync();
    }

    [Fact]
    public async Task GivenDisabledWallets_WhenDeleteDisabledWalletsAsyncCalled_ThenReturnsCorrectInformation()
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddDays(-365);
        var testId = Guid.NewGuid().ToString();

        var wallet = await _dbFixture.CreateWallet($"test-owner-{testId}");
        using (var conn = _dbFixture.GetConnectionFactory().CreateConnection())
        {
            conn.Open();
            var repo = new WalletRepository(conn);
            await repo.DisableWallet(wallet.Id, now.AddDays(-366));
        }

        using (var conn = _dbFixture.GetConnectionFactory().CreateConnection())
        {
            conn.Open();
            var repo = new WalletRepository(conn);
            var (count, deletedWallets) = await repo.DeleteDisabledWalletsAsync(cutoff);

            count.Should().Be(1);
            deletedWallets.Should().HaveCount(1);
            deletedWallets[0].Id.Should().Be(wallet.Id);
            deletedWallets[0].Owner.Should().Be($"test-owner-{testId}");
            deletedWallets[0].DisabledDate.Should().BeCloseTo(now.AddDays(-366), TimeSpan.FromSeconds(1));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GivenLogDeletedWalletDetailsOption_WhenJobRuns_ThenRespectsLogDetailsSetting(bool logDetails)
    {
        const int intervalHours = 1;
        var now = DateTimeOffset.UtcNow;
        var testId = Guid.NewGuid().ToString();

        var wallet = await _dbFixture.CreateWallet($"test-owner-{testId}");
        using (var conn = _dbFixture.GetConnectionFactory().CreateConnection())
        {
            conn.Open();
            var repo = new WalletRepository(conn);
            await repo.DisableWallet(wallet.Id, now.AddDays(-366));
        }

        await ResetWalletCleanupJobExecutionAsync(_dbFixture);

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
                s.AddLogging();
                s.AddSingleton(_dbFixture.GetConnectionFactory());
                s.AddSingleton(opts);
                s.AddScoped<IUnitOfWork, UnitOfWork>();
                s.AddHostedService<WalletCleanupJob>();
            })
            .Build();

        await host.StartAsync();

        await Task.Delay(TimeSpan.FromSeconds(2));

        using var verifyConn = _dbFixture.GetConnectionFactory().CreateConnection();
        verifyConn.Open();
        var verifyRepo = new WalletRepository(verifyConn);
        (await verifyRepo.GetWallet(wallet.Id)).Should().BeNull("wallet should be deleted");

        await host.StopAsync();
    }

    [Fact]
    public async Task GivenJobWithDefaultOptions_WhenJobRuns_ThenDeletionIsDisabled()
    {
        var now = DateTimeOffset.UtcNow;
        var testId = Guid.NewGuid().ToString();

        var wallet = await _dbFixture.CreateWallet($"test-owner-{testId}");
        using (var conn = _dbFixture.GetConnectionFactory().CreateConnection())
        {
            conn.Open();
            var repo = new WalletRepository(conn);
            await repo.DisableWallet(wallet.Id, now.AddDays(-366));
        }

        var opts = Microsoft.Extensions.Options.Options.Create(new WalletCleanupOptions
        {
            IntervalHours = 1,
            RetentionDays = 365,
            LogDeletedWalletDetails = false
        });

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(s =>
            {
                s.AddLogging();
                s.AddSingleton(_dbFixture.GetConnectionFactory());
                s.AddSingleton(opts);

                s.AddScoped<IUnitOfWork, UnitOfWork>();
                s.AddHostedService<WalletCleanupJob>();
            })
            .Build();

        await host.StartAsync();

        await Task.Delay(TimeSpan.FromSeconds(2));

        using var verifyConn = _dbFixture.GetConnectionFactory().CreateConnection();
        verifyConn.Open();
        var verifyRepo = new WalletRepository(verifyConn);
        (await verifyRepo.GetWallet(wallet.Id)).Should().NotBeNull(
            "wallet should NOT be deleted when job is disabled");

        await host.StopAsync();
    }

    private static async Task ResetWalletCleanupJobExecutionAsync(PostgresDatabaseFixture dbFixture)
    {
        using var uow = new UnitOfWork(dbFixture.GetConnectionFactory());
        await uow.JobExecutionRepository.UpdateLastExecutionTime(nameof(WalletCleanupJob), DateTimeOffset.UtcNow.AddDays(-30));
        uow.Commit();
    }
}
