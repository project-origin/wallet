using FluentAssertions;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class UnitOfWorkTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly IHDAlgorithm _algorithm;

    public UnitOfWorkTests(PostgresDatabaseFixture fixture)
    {
        _algorithm = new Secp256k1Algorithm();
        _dbFixture = fixture;
    }

    [Fact]
    public async Task Create_Commit_Expect1()
    {
        var owner = Guid.NewGuid().ToString();
        var model = new Wallet
        {
            Id = Guid.NewGuid(),
            Owner = owner,
            PrivateKey = _algorithm.GenerateNewPrivateKey()
        };

        var dbConnectionFactory = _dbFixture.GetConnectionFactory();

        using (var uof = new UnitOfWork(dbConnectionFactory))
        {
            await uof.WalletRepository.Create(model);
            uof.Commit();
        };

        using (var uof = new UnitOfWork(dbConnectionFactory))
        {
            var data = await uof.WalletRepository.GetWallet(owner);
            data.Should().NotBeNull();
            data!.Owner.Should().Be(owner);
        }
    }

    [Fact]
    public async Task Create_Rollback_Still_Expect1()
    {
        var owner = Guid.NewGuid().ToString();
        var model = new Wallet
        {
            Id = Guid.NewGuid(),
            Owner = owner,
            PrivateKey = _algorithm.GenerateNewPrivateKey()
        };

        var dbConnectionFactory = _dbFixture.GetConnectionFactory();

        using (var uof = new UnitOfWork(dbConnectionFactory))
        {
            var wallet = await uof.WalletRepository.GetWallet(owner);
            wallet.Should().BeNull();

            await uof.WalletRepository.Create(model);

            wallet = await uof.WalletRepository.GetWallet(owner);
            wallet.Should().NotBeNull();

            uof.Rollback();

            wallet = await uof.WalletRepository.GetWallet(owner);
            wallet.Should().BeNull();
        };

        using (var uof = new UnitOfWork(dbConnectionFactory))
        {
            var data = await uof.WalletRepository.GetWallet(owner);
            data.Should().BeNull();
        }
    }
}
