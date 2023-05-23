using DbUp;

namespace ProjectOrigin.WalletSystem.Server.Database;

public static class DatabaseUpgrader
{
    public static void Upgrade(string? connectionString)
    {
        var upgradeEngine = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseUpgrader).Assembly)
            .LogToAutodetectedLog()
            .Build();

        var databaseUpgradeResult = upgradeEngine.PerformUpgrade();

        if (!databaseUpgradeResult.Successful)
        {
            throw databaseUpgradeResult.Error;
        }
    }
}
