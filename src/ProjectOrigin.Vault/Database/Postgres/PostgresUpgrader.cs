using System;
using System.Threading.Tasks;
using DbUp;
using DbUp.Engine;
using DbUp.Engine.Output;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ProjectOrigin.Vault.Database.Postgres;

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

    public async Task UpgradeToTarget(string? target)
    {
        var filter = (string scriptName) =>
        {
            return scriptName.ToLower().EndsWith(".sql") &&
                   (target is null || String.Compare(scriptName.ToLower(), target.ToLower(), StringComparison.Ordinal) <= 0);
        };

        var upgradeEngine = DeployChanges.To
            .PostgresqlDatabase(_connectionString)
            .WithTransactionPerScript()
            .WithScriptsEmbeddedInAssembly(typeof(PostgresUpgrader).Assembly, filter)
            .LogTo(new LoggerWrapper(_logger))
            .WithExecutionTimeout(_timeout)
            .Build();

        await TryConnectToDatabaseWithRetry(upgradeEngine);

        EnsureDatabase.For.PostgresqlDatabase(_connectionString);

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
            _logger.LogWarning("Failed to connect to database ({message}), waiting to retry in {sleepTime} seconds... ", msg, _sleepTime.TotalSeconds);
            await Task.Delay(_sleepTime);

            if (DateTime.UtcNow - started > _timeout)
                throw new TimeoutException($"Could not connect to database ({msg}), exceeded retry limit.");
        }
    }

    private UpgradeEngine BuildUpgradeEngine(string? connectionString)
    {
        return DeployChanges.To
                    .PostgresqlDatabase(connectionString)
                    .WithTransaction()
                    .WithScriptsEmbeddedInAssembly(typeof(PostgresUpgrader).Assembly)
                    .LogTo(new LoggerWrapper(_logger))
                    .WithExecutionTimeout(_timeout)
                    .Build();
    }

    private sealed class LoggerWrapper : IUpgradeLog
    {
        private readonly ILogger _logger;

        public LoggerWrapper(ILogger logger)
        {
            _logger = logger;
        }

        public void LogInformation(string format, params object[] args)
        {
            _logger.LogInformation(format, args);
        }

        public void LogDebug(string format, params object[] args)
        {
            _logger.LogDebug(format, args);
        }

        public void LogTrace(string format, params object[] args)
        {
            _logger.LogTrace(format, args);
        }

        public void LogWarning(string format, params object[] args)
        {
            _logger.LogWarning(format, args);
        }

        public void LogError(string format, params object[] args)
        {
            _logger.LogError(format, args);
        }

        public void LogError(Exception ex, string format, params object[] args)
        {
            _logger.LogError(ex, format, args);
        }
    }
}
