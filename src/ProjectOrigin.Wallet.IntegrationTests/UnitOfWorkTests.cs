using FluentAssertions;
using ProjectOrigin.Wallet.Server.Database;
using ProjectOrigin.Wallet.Server.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.Wallet.IntegrationTests;

public class UnitOfWorkTests : IClassFixture<PostgresDatabaseFixture>
{
    private PostgresDatabaseFixture _dbFixture;

    public UnitOfWorkTests(PostgresDatabaseFixture fixture)
    {
        this._dbFixture = fixture;
    }

    [Fact]
    public async Task Create_Commit_ExpectNotNull()
    {
        var model = new MyTable(1, Guid.NewGuid().ToString());

        var dbConnectionFactory = new DbConnectionFactory(_dbFixture.ConnectionString);

        using (var uof = new UnitOfWork(dbConnectionFactory))
        {
            await uof.WalletRepository.Create(model);
            uof.Commit();
        };

        using (var uof = new UnitOfWork(dbConnectionFactory))
        {
            var obj = await uof.WalletRepository.Get(model.Id);
            obj.Should().NotBeNull();
            obj!.Foo.Should().Be(model.Foo);
        }
    }

    [Fact]
    public async Task Create_Rollback_Still_ExpectNull()
    {
        var model = new MyTable(2, Guid.NewGuid().ToString());

        var dbConnectionFactory = new DbConnectionFactory(_dbFixture.ConnectionString);

        using (var uof = new UnitOfWork(dbConnectionFactory))
        {
            var obj = await uof.WalletRepository.Get(model.Id);
            obj.Should().BeNull();

            await uof.WalletRepository.Create(model);

            obj = await uof.WalletRepository.Get(model.Id);
            obj.Should().NotBeNull();

            uof.Rollback();

            obj = await uof.WalletRepository.Get(model.Id);
            obj.Should().BeNull();
        };

        using (var uof = new UnitOfWork(dbConnectionFactory))
        {
            var obj = await uof.WalletRepository.Get(model.Id);
            obj.Should().BeNull();
        }
    }
}
