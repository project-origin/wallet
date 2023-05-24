using DbUp;
using DbUp.Engine;

namespace ProjectOrigin.WalletSystem.Server.Database;

public static class DatabaseUpgrader
{
    public static void Upgrade(string? connectionString)
    {
        var upgradeEngine = BuildUpgradeEngine(connectionString);

        var databaseUpgradeResult = upgradeEngine.PerformUpgrade();

        if (!databaseUpgradeResult.Successful)
        {
            throw databaseUpgradeResult.Error;
        }
    }

    public static bool IsUpgradeRequired(string? connectionString)
    {
        var upgradeEngine = BuildUpgradeEngine(connectionString);

        return upgradeEngine.IsUpgradeRequired();
    }

    private static UpgradeEngine BuildUpgradeEngine(string? connectionString)
    {
        return DeployChanges.To
                    .PostgresqlDatabase(connectionString)
                    .WithScriptsEmbeddedInAssembly(typeof(DatabaseUpgrader).Assembly)
                    .LogToAutodetectedLog()
                    .Build();
    }
}
