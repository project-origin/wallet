using System.Threading.Tasks;
using Testcontainers.PostgreSql;
using Xunit;

namespace ProjectOrigin.Wallet.IntegrationTests;

public class PostgresDatabaseFixture : IAsyncLifetime
{
    public string ConnectionString => _postgreSqlContainer.GetConnectionString();

    private PostgreSqlContainer _postgreSqlContainer;

    public PostgresDatabaseFixture()
    {
        _postgreSqlContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15")
            .Build();

        _postgreSqlContainer.StartAsync().Wait();
    }

    public virtual async Task InitializeAsync()
    {
        await _postgreSqlContainer.StartAsync();

    }

    public virtual Task DisposeAsync()
    {
        return _postgreSqlContainer.StopAsync();
    }
}
