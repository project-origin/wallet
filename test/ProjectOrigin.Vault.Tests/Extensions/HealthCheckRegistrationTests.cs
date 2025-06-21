using System;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ProjectOrigin.Vault.Tests.Extensions;

public class HealthChecksRegistrationTests
{
    [Fact]
    public void AddHealthChecks_ThrowsException_WhenDatabaseConnectionStringIsNull()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        Action act = () => services.AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("Database") ?? throw new InvalidOperationException());

        act.Should().Throw<InvalidOperationException>();
    }
}
