using Dapper;
using FluentAssertions;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.Vault.Tests;

public class MigrationTest : IClassFixture<PostgresDatabaseFixture>
{
    private readonly PostgresDatabaseFixture _dbFixture;

    public MigrationTest(PostgresDatabaseFixture fixture)
    {
        _dbFixture = fixture;
    }

    [Fact]
    public async Task can_insert_and_query_after_migration()
    {
        // Arrange
        var wallet = new
        {
            Id = Guid.NewGuid(),
            Owner = Guid.NewGuid().ToString(),
            PrivateKey = new byte[] { 1, 2, 3 }
        };

        using var connection = _dbFixture.GetConnectionFactory().CreateConnection();

        // Act
        await connection.ExecuteAsync(
            "INSERT INTO wallets(id, owner, private_key) VALUES (@Id, @Owner, @PrivateKey)",
            wallet);

        var count = await connection.ExecuteScalarAsync("SELECT count(*) FROM wallets");

        // Assert
        count.Should().Be(1);
    }
}
