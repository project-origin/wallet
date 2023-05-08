using Dapper;
using FluentAssertions;
using ProjectOrigin.Wallet.Server;
using ProjectOrigin.Wallet.Server.Database;
using System;
using System.Threading.Tasks;
using ProjectOrigin.Wallet.Server.Models;
using Testcontainers.PostgreSql;
using Xunit;

namespace ProjectOrigin.Wallet.IntegrationTests;

public class DatabaseTests
{
    [Fact]
    public async Task can_insert_and_query_after_migration()
    {
        var postgreSqlContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15")
            .Build();

        await postgreSqlContainer.StartAsync();

        DatabaseUpgrader.Upgrade(postgreSqlContainer.GetConnectionString());

        var dapperContext = new DapperContext(postgreSqlContainer.GetConnectionString());
        using var connection = dapperContext.CreateConnection();

        await connection.ExecuteAsync(@"INSERT INTO MyTable(Foo) VALUES (@foo)", new { foo = Guid.NewGuid().ToString() });

        var myTables = await connection.QueryAsync<MyTable>("SELECT * FROM MyTable");

        myTables.Should().HaveCount(1);

        await postgreSqlContainer.StopAsync();
    }
}
