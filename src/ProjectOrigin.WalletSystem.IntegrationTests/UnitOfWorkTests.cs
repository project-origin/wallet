using Dapper;
using FluentAssertions;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Database.Mapping;
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

        SqlMapper.AddTypeHandler(new HDPrivateKeyTypeHandler(_algorithm));
        SqlMapper.AddTypeHandler(new HDPublicKeyTypeHandler(_algorithm));
    }

    [Fact]
    public async Task Create_Commit_Expect1()
    {
        var owner = Guid.NewGuid().ToString();
        var model = new Wallet(Guid.NewGuid(), owner, _algorithm.GenerateNewPrivateKey());

        var dbConnectionFactory = new DbConnectionFactory(_dbFixture.ConnectionString);

        using (var uof = new UnitOfWork(dbConnectionFactory))
        {
            await uof.WalletRepository.Create(model);
            uof.Commit();
        };

        using (var uof = new UnitOfWork(dbConnectionFactory))
        {
            var data = await uof.WalletRepository.GetWalletByOwner(owner);
            data.Should().NotBeNull();
            data!.Owner.Should().Be(owner);
        }
    }

    [Fact]
    public async Task Create_Rollback_Still_Expect1()
    {
        var owner = Guid.NewGuid().ToString();
        var model = new Wallet(Guid.NewGuid(), owner, _algorithm.GenerateNewPrivateKey());

        var dbConnectionFactory = new DbConnectionFactory(_dbFixture.ConnectionString);

        using (var uof = new UnitOfWork(dbConnectionFactory))
        {
            var wallet = await uof.WalletRepository.GetWalletByOwner(owner);
            wallet.Should().BeNull();

            await uof.WalletRepository.Create(model);

            wallet = await uof.WalletRepository.GetWalletByOwner(owner);
            wallet.Should().NotBeNull();

            uof.Rollback();

            wallet = await uof.WalletRepository.GetWalletByOwner(owner);
            wallet.Should().BeNull();
        };

        using (var uof = new UnitOfWork(dbConnectionFactory))
        {
            var data = await uof.WalletRepository.GetWalletByOwner(owner);
            data.Should().BeNull();
        }
    }
}
