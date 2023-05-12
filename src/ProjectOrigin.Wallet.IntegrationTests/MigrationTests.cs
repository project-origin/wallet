using Dapper;
using FluentAssertions;
using ProjectOrigin.Wallet.Server.Database;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.Wallet.IntegrationTests;

public class MigrationTest : IClassFixture<PostgresDatabaseFixture>
{
    private PostgresDatabaseFixture _dbFixture;

    public MigrationTest(PostgresDatabaseFixture fixture)
    {
        _dbFixture = fixture;
    }

    [Fact]
    public async Task can_insert_and_query_after_migration()
    {
        // Arrange
        DatabaseUpgrader.Upgrade(_dbFixture.ConnectionString);

        var wallet = new
        {
            Id = Guid.NewGuid(),
            Owner = Guid.NewGuid().ToString(),
            PrivateKey = new byte[] { 1, 2, 3 }
        };

        using var connection = new DbConnectionFactory(_dbFixture.ConnectionString).CreateConnection();

        // Act
        await connection.ExecuteAsync(
            "INSERT INTO Wallets(Id, Owner, PrivateKey) VALUES (@Id, @Owner, @PrivateKey)",
            wallet);

        var count = await connection.ExecuteScalarAsync("SELECT count(*) FROM Wallets");

        // Assert
        count.Should().Be(1);
    }
}
