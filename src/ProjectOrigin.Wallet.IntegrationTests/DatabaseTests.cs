using Dapper;
using DbUp;
using FluentAssertions;
using Npgsql;
using ProjectOrigin.Wallet.Server;
using ProjectOrigin.Wallet.V1;
using System;
using System.Threading.Tasks;
using Testcontainers.PostgreSql;
using Xunit;

namespace ProjectOrigin.Wallet.IntegrationTests;

public class DatabaseTests
{
    [Fact]
    public async Task can_insert_and_query_after_migration()
    {
        var postgreSqlContainer = new PostgreSqlBuilder()
            .Build();

        await postgreSqlContainer.StartAsync();

        var upgradeEngine = DeployChanges.To
            .PostgresqlDatabase(postgreSqlContainer.GetConnectionString())
            .WithScriptsEmbeddedInAssembly(typeof(WalletService).Assembly)
            .LogToAutodetectedLog()
            .Build();

        var databaseUpgradeResult = upgradeEngine.PerformUpgrade();
        if (!databaseUpgradeResult.Successful)
        {
            throw databaseUpgradeResult.Error;
        }

        await using var connection = new NpgsqlConnection(postgreSqlContainer.GetConnectionString());

        await connection.ExecuteAsync(@"INSERT INTO MyTable(Foo) VALUES (@foo)", new { foo = Guid.NewGuid().ToString() });

        var myTables = await connection.QueryAsync<MyTable>("SELECT * FROM MyTable");

        myTables.Should().HaveCount(1);

        await postgreSqlContainer.StopAsync();
    }
}
