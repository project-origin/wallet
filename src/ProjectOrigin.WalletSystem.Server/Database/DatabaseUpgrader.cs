using System;
using System.Threading.Tasks;
using DbUp;
using DbUp.Engine;

namespace ProjectOrigin.WalletSystem.Server.Database;

public static class DatabaseUpgrader
{
    private static TimeSpan _sleepTime = TimeSpan.FromSeconds(5);
    private static TimeSpan _defaultTimeout = TimeSpan.FromMinutes(5);

    public static async Task Upgrade(string? connectionString)
    {
        var upgradeEngine = BuildUpgradeEngine(connectionString);

        await TryConnectToDatabaseWithRetry(upgradeEngine);

        Console.WriteLine($"Performing database upgrade.");
        var databaseUpgradeResult = upgradeEngine.PerformUpgrade();

        if (databaseUpgradeResult.Successful)
        {
            Console.WriteLine($"Database upgraded successfully.");
        }
        else
        {
            Console.WriteLine($"Failed to upgrade database - {databaseUpgradeResult.Error.Message}");
            throw databaseUpgradeResult.Error;
        }
    }

    public static bool IsUpgradeRequired(string? connectionString)
    {
        var upgradeEngine = BuildUpgradeEngine(connectionString);

        return upgradeEngine.IsUpgradeRequired();
    }

    private static async Task TryConnectToDatabaseWithRetry(UpgradeEngine upgradeEngine)
    {
        var started = DateTime.UtcNow;

        while (!upgradeEngine.TryConnect(out string msg))
        {
            Console.WriteLine($"Failed to connect to database ({msg}), waiting to retry in {_sleepTime.TotalSeconds} seconds... ");
            await Task.Delay(_sleepTime);
            if (DateTime.UtcNow - started > _defaultTimeout)
                throw new TimeoutException($"Could not connect to database ({msg}), exceeded retry limit.");
        }
        Console.WriteLine($"Successfully connected to database.");
    }

    private static UpgradeEngine BuildUpgradeEngine(string? connectionString)
    {
        return DeployChanges.To
                    .PostgresqlDatabase(connectionString)
                    .WithScriptsEmbeddedInAssembly(typeof(DatabaseUpgrader).Assembly)
                    .LogToAutodetectedLog()
                    .WithExecutionTimeout(TimeSpan.FromMinutes(5))
                    .Build();
    }
}
