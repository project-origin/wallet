using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.Server.Database;
using Testcontainers.PostgreSql;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class PostgresDatabaseFixture : IAsyncLifetime
{
    public string ConnectionString => _postgreSqlContainer.GetConnectionString();

    private PostgreSqlContainer _postgreSqlContainer;

    public PostgresDatabaseFixture()
    {
        _postgreSqlContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgreSqlContainer.StartAsync();
        await DatabaseUpgrader.Upgrade(_postgreSqlContainer.GetConnectionString());
    }

    public Task DisposeAsync()
    {
        return _postgreSqlContainer.StopAsync();
    }
}
