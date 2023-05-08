using System.Reflection;
using DbUp;

namespace ProjectOrigin.Wallet.Server;

public static class DatabaseUpgrader
{
    public static void Upgrade(string? connectionString)
    {
        var upgradeEngine = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseUpgrader).Assembly)
            //.WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .LogToAutodetectedLog()
            .Build();

        var databaseUpgradeResult = upgradeEngine.PerformUpgrade();
        if (!databaseUpgradeResult.Successful)
        {
            throw databaseUpgradeResult.Error;
        }
    }
}
