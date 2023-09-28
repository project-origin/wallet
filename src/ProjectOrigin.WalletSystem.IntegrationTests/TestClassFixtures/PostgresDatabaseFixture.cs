using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Database.Mapping;
using ProjectOrigin.WalletSystem.Server.Database.Postgres;
using Testcontainers.PostgreSql;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;

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
        Dapper.SqlMapper.AddTypeHandler(new HDPrivateKeyTypeHandler(algorithm));
        Dapper.SqlMapper.AddTypeHandler(new HDPublicKeyTypeHandler(algorithm));
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
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
            Options.Create(new PostgresOptions
            {
                ConnectionString = _postgreSqlContainer.GetConnectionString()
            }));

        await upgrader.Upgrade();
    }

    public IDbConnectionFactory GetConnectionFactory() => new PostgresConnectionFactory(Options.Create(new PostgresOptions
    {
        ConnectionString = _postgreSqlContainer.GetConnectionString()
    }));

    public Task DisposeAsync()
    {
        return _postgreSqlContainer.StopAsync();
    }
}
