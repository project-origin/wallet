using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace ProjectOrigin.Vault.Tests.Extensions;

public class HealthChecksRegistrationTests
{
    [Fact]
    public void Should_register_health_checks()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new KeyValuePair<string, string?>("ConnectionStrings:Database", "Host=localhost;Username=postgres;Password=postgres")
            ])
            .Build();

        var healthChecks = services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy());

        healthChecks.AddNpgSql(
            configuration.GetConnectionString("Database") ?? throw new InvalidOperationException(),
            name: "postgres",
            tags: ["ready", "db"]);

        var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<HealthCheckService>();
    }
}
