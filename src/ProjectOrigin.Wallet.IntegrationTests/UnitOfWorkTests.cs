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
        DatabaseUpgrader.Upgrade(fixture.ConnectionString);
    }

    [Fact]
    public async Task Create_Commit_Expect1()
    {
        var model = new MyTable(0, Guid.NewGuid().ToString());

        var dbConnectionFactory = new DbConnectionFactory(_dbFixture.ConnectionString);

        using (var uof = new UnitOfWork(dbConnectionFactory))
        {
            await uof.WalletRepository.Create(model);

            var data = await uof.WalletRepository.GetAll();
            uof.Commit();
        };

        using (var uof = new UnitOfWork(dbConnectionFactory))
        {
            var data = await uof.WalletRepository.GetAll();
            data.Should().HaveCount(1);
            data.First().Foo.Should().Be(model.Foo);
        }
    }

    [Fact]
    public async Task Create_Rollback_Still_Expect1()
    {
        var model = new MyTable(0, Guid.NewGuid().ToString());

        var dbConnectionFactory = new DbConnectionFactory(_dbFixture.ConnectionString);

        using (var uof = new UnitOfWork(dbConnectionFactory))
        {
            var data = await uof.WalletRepository.GetAll();
            data.Should().HaveCount(1);

            await uof.WalletRepository.Create(model);

            data = await uof.WalletRepository.GetAll();
            data.Should().HaveCount(2);

            uof.Rollback();

            data = await uof.WalletRepository.GetAll();
            data.Should().HaveCount(1);
        };

        using (var uof = new UnitOfWork(dbConnectionFactory))
        {
            var data = await uof.WalletRepository.GetAll();
            data.Should().HaveCount(1);
        }
    }
}
