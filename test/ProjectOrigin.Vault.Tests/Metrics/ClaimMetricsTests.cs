using System;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using ProjectOrigin.Vault.Metrics;
using Xunit;

namespace ProjectOrigin.Vault.Tests.Metrics;

public class ClaimMetricsTests
{
    private readonly IClaimMetrics _claimMetrics;

    private readonly MetricCollector<long> _claimIntentsCollector;
    private readonly MetricCollector<long> _claimsClaimedCollector;

    public ClaimMetricsTests()
    {
        var services = new ServiceCollection();

        services.AddMetrics();

        services.AddSingleton(CreateConfiguration());

        services.AddSingleton<MeterBase>();
        services.AddSingleton<IClaimMetrics, ClaimMetrics>();

        IServiceProvider provider = services.BuildServiceProvider();

        var meterFactory = provider.GetRequiredService<IMeterFactory>();
        _claimMetrics = provider.GetRequiredService<IClaimMetrics>();

        _claimsClaimedCollector = new MetricCollector<long>(meterFactory, "ProjectOrigin.Vault", "po.vault.claim.certificate.claimed.count");
        _claimIntentsCollector = new MetricCollector<long>(meterFactory, "ProjectOrigin.Vault", "po.vault.claim.certificate.intent.received.count");
    }

    [Fact]
    public void IncrementClaimed_ShouldUpdateCounter()
    {
        _claimMetrics.IncrementClaimed();
        _claimsClaimedCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(1);
    }

    [Fact]
    public void IncrementIntents_ShouldUpdateCounter()
    {
        _claimMetrics.IncrementClaimIntents();
        _claimIntentsCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(1);
    }

    [Fact]
    public void IncrementClaimed_ShouldNotAffectIntentsCounter()
    {
        _claimMetrics.IncrementClaimed();
        _claimIntentsCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(0);
    }

    [Fact]
    public void IncrementIntents_ShouldNotAffectRejectedCounter()
    {
        _claimMetrics.IncrementClaimIntents();
        _claimsClaimedCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(0);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();
    }
}
