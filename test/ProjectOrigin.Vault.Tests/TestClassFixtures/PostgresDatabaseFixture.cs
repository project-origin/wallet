using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MsOptions = Microsoft.Extensions.Options;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Database.Mapping;
using ProjectOrigin.Vault.Database.Postgres;
using Testcontainers.PostgreSql;
using Xunit;

namespace ProjectOrigin.Vault.Tests.TestClassFixtures;

public class PostgresDatabaseFixture : IAsyncLifetime
{
    public string ConnectionString => _postgreSqlContainer.GetConnectionString();

    private PostgreSqlContainer _postgreSqlContainer;

    public PostgresDatabaseFixture()
    {
        _postgreSqlContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15")
            .Build();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });

        var algorithm = new Secp256k1Algorithm();
        ApplicationBuilderExtension.ConfigureMappers(algorithm);
    }

    public async Task InitializeAsync()
    {
        await _postgreSqlContainer.StartAsync();
        await UpgradeDatabase();
    }

    public async Task UpgradeDatabase()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });

        var upgrader = new PostgresUpgrader(
            loggerFactory.CreateLogger<PostgresUpgrader>(),
            MsOptions.Options.Create(new PostgresOptions
            {
                ConnectionString = _postgreSqlContainer.GetConnectionString()
            }));

        await upgrader.Upgrade();
    }

    public IDbConnectionFactory GetConnectionFactory() => new PostgresConnectionFactory(MsOptions.Options.Create(new PostgresOptions
    {
        ConnectionString = _postgreSqlContainer.GetConnectionString()
    }));

    public Task DisposeAsync()
    {
        return _postgreSqlContainer.StopAsync();
    }
}
