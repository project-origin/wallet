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

    private readonly MetricCollector<long> _countCollector;
    private readonly MetricCollector<long> _claimedCollector;
    private readonly MetricCollector<long> _rejectedCollector;

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

        _countCollector = new MetricCollector<long>(meterFactory, "ProjectOrigin.Vault", "po.vault.claim.certificate.count");
        _claimedCollector = new MetricCollector<long>(meterFactory, "ProjectOrigin.Vault", "po.vault.claim.certificate.claimed.count");
        _rejectedCollector = new MetricCollector<long>(meterFactory, "ProjectOrigin.Vault", "po.vault.claim.certificate.rejected.count");
    }

    [Fact]
    public void IncrementClaimed_ShouldUpdateCounters()
    {
        _claimMetrics.IncrementClaimed();

        _countCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(1);
        _claimedCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(1);
    }

    [Fact]
    public void IncrementRejected_ShouldUpdateCounters()
    {
        _claimMetrics.IncrementRejected();

        _countCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(1);
        _rejectedCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(1);
    }

    [Fact]
    public void IncrementClaimed_ShouldNotAffectRejectedCounters()
    {
        _claimMetrics.IncrementClaimed();

        _rejectedCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(0);
    }

    [Fact]
    public void IncrementRejected_ShouldNotAffectClaimedCounters()
    {
        _claimMetrics.IncrementRejected();

        _claimedCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(0);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();
    }
}
