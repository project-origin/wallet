using Dapper;
using FluentAssertions;
using ProjectOrigin.Wallet.Server.Database;
using ProjectOrigin.Wallet.Server.Models;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.Wallet.IntegrationTests;

public class MigrationTest : IClassFixture<PostgresDatabaseFixture>
{
    private PostgresDatabaseFixture _dbFixture;

    public MigrationTest(PostgresDatabaseFixture fixture)
    {
        this._dbFixture = fixture;
    }

    [Fact]
    public async Task can_insert_and_query_after_migration()
    {
        DatabaseUpgrader.Upgrade(_dbFixture.ConnectionString);

        using var connection = new DbConnectionFactory(_dbFixture.ConnectionString).CreateConnection();

        await connection.ExecuteAsync(@"INSERT INTO MyTable(Foo) VALUES (@foo)", new { foo = Guid.NewGuid().ToString() });

        var myTables = await connection.QueryAsync<MyTable>("SELECT * FROM MyTable");

        myTables.Should().HaveCount(1);
    }
}
