using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.Vault.Jobs;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Options;
using ProjectOrigin.Vault.Repositories;
using Xunit;

namespace ProjectOrigin.Vault.Tests.JobTests;

public sealed class WalletCleanupWorkerFakeTimeIntegrationTests
{
    private sealed class InMemoryWalletRepo : IWalletRepository
    {
        private readonly ConcurrentDictionary<string, DateTimeOffset?> _store = new();

        public void Add(string owner, DateTimeOffset? disabledUtc)
            => _store[owner] = disabledUtc;

        public Task<int> DeleteDisabledWalletsAsync(DateTimeOffset cutoffUtc)
        {
            var removed = 0;
            foreach (var (owner, disabled) in _store)
            {
                if (disabled is not null && disabled < cutoffUtc)
                {
                    _store.TryRemove(owner, out _);
                    removed++;
                }
            }
            return Task.FromResult(removed);
        }

        public bool Exists(string owner) => _store.ContainsKey(owner);

        #region unused interface members
        public Task<int> Create(Wallet _) => Task.FromResult(0);
        public Task<Wallet?> GetWallet(Guid _) => Task.FromResult<Wallet?>(null);
        public Task<Wallet?> GetWallet(string _) => Task.FromResult<Wallet?>(null);
        public Task<WalletEndpoint> CreateWalletEndpoint(Guid _) => Task.FromResult<WalletEndpoint>(default!);
        public Task<WalletEndpoint?> GetWalletEndpoint(IHDPublicKey _) => Task.FromResult<WalletEndpoint?>(null);
        public Task<WalletEndpoint> GetWalletEndpoint(Guid _) => Task.FromResult<WalletEndpoint>(default!);
        public Task<WalletEndpoint> GetWalletRemainderEndpoint(Guid _) => Task.FromResult<WalletEndpoint>(default!);
        public Task<int> GetNextNumberForId(Guid _) => Task.FromResult(0);
        public Task<IHDPrivateKey> GetPrivateKeyForSlice(Guid _) => Task.FromResult<IHDPrivateKey>(default!);
        public Task<ExternalEndpoint> CreateExternalEndpoint(string _, IHDPublicKey __, string ___, string ____) => Task.FromResult<ExternalEndpoint>(default!);
        public Task<ExternalEndpoint> GetExternalEndpoint(Guid _) => Task.FromResult<ExternalEndpoint>(default!);
        public Task<ExternalEndpoint?> TryGetExternalEndpoint(Guid _) => Task.FromResult<ExternalEndpoint?>(null);
        public Task EnableWallet(Guid _) => Task.CompletedTask;
        public Task DisableWallet(Guid _, DateTimeOffset __) => Task.CompletedTask;
        #endregion
    }

    [Fact]
    public async Task Worker_deletes_wallets_past_retention_and_leaves_recent_ones()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var repo = new InMemoryWalletRepo();

        var opts = Microsoft.Extensions.Options.Options.Create(new WalletCleanupOptions
        {
            IntervalHours = 1,
            RetentionDays = 10
        });

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(s =>
            {
                s.AddSingleton(new LoggerFactory().CreateLogger<WalletCleanupWorker>());
                s.AddSingleton(repo);
                s.AddSingleton<IWalletRepository>(repo);
                s.AddSingleton(opts);
                s.AddSingleton<TimeProvider>(fakeTime);
                s.AddHostedService<WalletCleanupWorker>();
            })
            .Build();

        await host.StartAsync();

        repo.Add("old-owner", fakeTime.GetUtcNow().AddDays(-11));
        repo.Add("recent-owner", fakeTime.GetUtcNow().AddDays(-2));

        fakeTime.Advance(TimeSpan.FromHours(2));

        await Task.Yield();

        repo.Exists("old-owner").Should().BeFalse();
        repo.Exists("recent-owner").Should().BeTrue();

        await host.StopAsync();
    }
}
