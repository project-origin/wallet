using System;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Repositories;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using Xunit;

namespace ProjectOrigin.Vault.Tests.MigrationTests;

public class V20012 : IClassFixture<PostgresDatabaseMigrationFixture>
{
    private readonly PostgresDatabaseMigrationFixture _dbFixture;

    public V20012(PostgresDatabaseMigrationFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    [Fact]
    public async Task CanMigrate()
    {
        await _dbFixture.UpgradeDatabaseToTarget("v2-0011.sql");

        var requestId = Guid.NewGuid();
        var owner = Guid.NewGuid().ToString();

        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            await connection.ExecuteAsync(
                @"INSERT INTO request_statuses(request_id, owner, status, failed_reason)
                VALUES (@requestId, @owner, @status, @failedReason)",
                new
                {
                    requestId,
                    owner,
                    status = 1,
                    failedReason = "Some reason"
                });
        }

        await _dbFixture.UpgradeDatabaseToTarget("v2-0012.sql");

        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var repo = new RequestStatusRepository(connection);

            var requestStatus = await repo.GetRequestStatus(requestId, owner);

            Assert.Equal(RequestStatusType.Unknown, requestStatus!.Type);
        }
    }
}
