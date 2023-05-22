using Dapper;
using FluentAssertions;
using ProjectOrigin.Wallet.Server.Database;
using ProjectOrigin.Wallet.Server.Database.Mapping;
using ProjectOrigin.Wallet.Server.HDWallet;
using ProjectOrigin.Wallet.Server.Models;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.Wallet.IntegrationTests;

public class UnitOfWorkTests : IClassFixture<PostgresDatabaseFixture>
{
    private PostgresDatabaseFixture _dbFixture;
    private IHDAlgorithm _algorithm;

    public UnitOfWorkTests(PostgresDatabaseFixture fixture)
    {
        this._algorithm = new Secp256k1Algorithm();
        this._dbFixture = fixture;
        DatabaseUpgrader.Upgrade(fixture.ConnectionString);

        SqlMapper.AddTypeHandler<IHDPrivateKey>(new HDPrivateKeyTypeHandler(this._algorithm));
        SqlMapper.AddTypeHandler<IHDPublicKey>(new HDPublicKeyTypeHandler(this._algorithm));
    }

    [Fact]
    public async Task Create_Commit_Expect1()
    {
        var owner = Guid.NewGuid().ToString();
        var model = new OwnerWallet(Guid.NewGuid(), owner, this._algorithm.GenerateNewPrivateKey());

        var dbConnectionFactory = new DbConnectionFactory(_dbFixture.ConnectionString);

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
        var model = new OwnerWallet(Guid.NewGuid(), owner, this._algorithm.GenerateNewPrivateKey());

        var dbConnectionFactory = new DbConnectionFactory(_dbFixture.ConnectionString);

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
