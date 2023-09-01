using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DbUp;
using DbUp.Engine;

namespace ProjectOrigin.WalletSystem.Server.Database;

public static class DatabaseUpgrader
{
    private static TimeSpan _sleepTime = TimeSpan.FromSeconds(5);
    private static TimeSpan _connectTimeout = TimeSpan.FromSeconds(15);
    private static TimeSpan _longTimeout = TimeSpan.FromMinutes(5);

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
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var tryConnectTask = Task.Run(() =>
            {
                var success = upgradeEngine.TryConnect(out string msg);
                return new DatabaseConnectResult(success, msg);
            });

            if (await Task.WhenAny(tryConnectTask, Task.Delay(_connectTimeout)) == tryConnectTask)
            {
                var result = await tryConnectTask;
                if (result.Success)
                {
                    Console.WriteLine($"Successfully connected to database.");
                    return;
                }
                else
                {
                    Console.WriteLine($"Failed to connect to database ({result.Message}), waiting to retry in {_sleepTime.TotalSeconds} seconds... ");
                }
            }
            else
            {
                Console.WriteLine($"Timeout while trying to connect to database, waiting to retry in {_sleepTime.TotalSeconds} seconds...");
            }

            if (stopwatch.Elapsed > _longTimeout)
                throw new TimeoutException($"Could not connect to database, exceeded retry limit.");

            await Task.Delay(_sleepTime);
        }
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

    record DatabaseConnectResult(bool Success, string Message);
}
