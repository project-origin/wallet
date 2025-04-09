using System;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.Vault.Repositories;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using Xunit;

namespace ProjectOrigin.Vault.Tests.MigrationTests;

public class V20014 : IClassFixture<PostgresDatabaseMigrationFixture>
{
    private readonly PostgresDatabaseMigrationFixture _dbFixture;

    public V20014(PostgresDatabaseMigrationFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    [Fact]
    public async Task CanMigrate()
    {
        await _dbFixture.UpgradeDatabaseToTarget("v2-0013.sql");

        var id = Guid.NewGuid();
        var owner = Guid.NewGuid().ToString();

        var algorithm = new Secp256k1Algorithm();

        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            await connection.ExecuteAsync(
                @"INSERT INTO wallets(id, owner, private_key)
                VALUES (@id, @owner, @privateKey)",
                new
                {
                    id,
                    owner,
                    privateKey = algorithm.GenerateNewPrivateKey()
                });
        }

        await _dbFixture.UpgradeDatabaseToTarget("v2-0014.sql");

        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var repo = new WalletRepository(connection);

            var walletDb = await repo.GetWallet(id);

            Assert.NotNull(walletDb);
            Assert.Equal(owner, walletDb.Owner);
            Assert.Null(walletDb.Disabled);
        }
    }
}
