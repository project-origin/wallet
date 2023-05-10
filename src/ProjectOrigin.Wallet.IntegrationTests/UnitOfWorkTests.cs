using FluentAssertions;
using ProjectOrigin.Wallet.Server.Database;
using ProjectOrigin.Wallet.Server.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.Wallet.IntegrationTests;

public class UnitOfWorkTests : AbstractPostgresTests
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        DatabaseUpgrader.Upgrade(_postgreSqlContainer.GetConnectionString());
    }

    [Fact]
    public async Task Create_Commit_Expect1()
    {
        var model = new MyTable(0, Guid.NewGuid().ToString());

        var dbConnectionFactory = new DbConnectionFactory(_postgreSqlContainer.GetConnectionString());

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
    public async Task Create_Rollback_Expect0()
    {
        var model = new MyTable(0, Guid.NewGuid().ToString());

        var dbConnectionFactory = new DbConnectionFactory(_postgreSqlContainer.GetConnectionString());

        using (var uof = new UnitOfWork(dbConnectionFactory))
        {
            await uof.WalletRepository.Create(model);

            var data = await uof.WalletRepository.GetAll();
            data.Should().HaveCount(1);

            uof.Rollback();

            data = await uof.WalletRepository.GetAll();
            data.Should().HaveCount(0);
        };

        using (var uof = new UnitOfWork(dbConnectionFactory))
        {
            var data = await uof.WalletRepository.GetAll();
            data.Should().HaveCount(0);
        }
    }


}
