using System;
using System.Threading.Tasks;
using DbUp;
using DbUp.Engine;
using DbUp.Engine.Output;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ProjectOrigin.WalletSystem.Server.Database.Postgres;

public class PostgresUpgrader : IRepositoryUpgrader
{
    private static TimeSpan _sleepTime = TimeSpan.FromSeconds(5);
    private static TimeSpan _timeout = TimeSpan.FromMinutes(5);
    private readonly ILogger<PostgresUpgrader> _logger;
    private readonly string _connectionString;

    public PostgresUpgrader(ILogger<PostgresUpgrader> logger, IOptions<PostgresOptions> configuration)
    {
        _logger = logger;
        _connectionString = configuration.Value.ConnectionString;
    }

    public async Task<bool> IsUpgradeRequired()
    {
        var upgradeEngine = BuildUpgradeEngine(_connectionString);
        await TryConnectToDatabaseWithRetry(upgradeEngine);

        return upgradeEngine.IsUpgradeRequired();
    }

    public async Task Upgrade()
    {
        var upgradeEngine = BuildUpgradeEngine(_connectionString);
        await TryConnectToDatabaseWithRetry(upgradeEngine);

        var databaseUpgradeResult = upgradeEngine.PerformUpgrade();

        if (!databaseUpgradeResult.Successful)
        {
            throw databaseUpgradeResult.Error;
        }
    }

    private async Task TryConnectToDatabaseWithRetry(UpgradeEngine upgradeEngine)
    {
        var started = DateTime.UtcNow;
        while (!upgradeEngine.TryConnect(out string msg))
        {
            _logger.LogWarning($"Failed to connect to database ({msg}), waiting to retry in {_sleepTime.TotalSeconds} seconds... ");
            await Task.Delay(_sleepTime);

            if (DateTime.UtcNow - started > _timeout)
                throw new TimeoutException($"Could not connect to database ({msg}), exceeded retry limit.");
        }
    }

    private UpgradeEngine BuildUpgradeEngine(string? connectionString)
    {
        return DeployChanges.To
                    .PostgresqlDatabase(connectionString)
                    .WithScriptsEmbeddedInAssembly(typeof(PostgresUpgrader).Assembly)
                    .LogTo(new LoggerWrapper(_logger))
                    .WithExecutionTimeout(_timeout)
                    .Build();
    }

    private class LoggerWrapper : IUpgradeLog
    {
        private ILogger _logger;

        public LoggerWrapper(ILogger logger)
        {
            _logger = logger;
        }

        public void WriteError(string format, params object[] args)
        {
            _logger.LogError(format, args);
        }

        public void WriteInformation(string format, params object[] args)
        {
            _logger.LogInformation(format, args);
        }

        public void WriteWarning(string format, params object[] args)
        {
            _logger.LogWarning(format, args);
        }
    }
}
