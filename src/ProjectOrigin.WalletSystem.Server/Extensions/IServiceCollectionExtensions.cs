using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Database.Postgres;

namespace ProjectOrigin.WalletSystem.Server.Extensions;

public static class IServiceCollectionExtensions
{
    public static void ConfigurePersistance(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IRepositoryUpgrader, PostgresUpgrader>();
        services.AddOptions<PostgresOptions>()
            .Configure(x => x.ConnectionString = configuration.GetConnectionString("Database")
                ?? throw new ArgumentNullException("Configuration does not contain a connection string named 'Database'."))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
