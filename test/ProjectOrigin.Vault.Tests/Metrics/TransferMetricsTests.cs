using System;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using ProjectOrigin.Vault.Metrics;
using Xunit;

namespace ProjectOrigin.Vault.Tests.Metrics;

public class TransferMetricsTests
{
    private readonly ITransferMetrics _transferMetrics;

    private readonly MetricCollector<long> _countCollector;
    private readonly MetricCollector<long> _completedCollector;
    private readonly MetricCollector<long> _rejectedCollector;

    public TransferMetricsTests()
    {
        var services = new ServiceCollection();
        services.AddMetrics();

        services.AddSingleton(CreateConfiguration());

        services.AddSingleton<MeterBase>();
        services.AddSingleton<ITransferMetrics, TransferMetrics>();

        IServiceProvider provider = services.BuildServiceProvider();

        var meterFactory = provider.GetRequiredService<IMeterFactory>();
        _transferMetrics = provider.GetRequiredService<ITransferMetrics>();

        _countCollector =
            new MetricCollector<long>(meterFactory, "ProjectOrigin.Vault", "po.vault.transfer.count");
        _completedCollector =
            new MetricCollector<long>(meterFactory, "ProjectOrigin.Vault", "po.vault.transfer.completed.count");
        _rejectedCollector =
            new MetricCollector<long>(meterFactory, "ProjectOrigin.Vault", "po.vault.transfer.rejected.count");
    }

    [Fact]
    public void IncrementCompleted_ShouldUpdateCounters()
    {
        _transferMetrics.IncrementCompleted();

        _countCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(1);
        _completedCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(1);
    }

    [Fact]
    public void IncrementRejected_ShouldUpdateCounters()
    {
        _transferMetrics.IncrementRejected();

        _countCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(1);
        _rejectedCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(1);
    }

    [Fact]
    public void IncrementCompleted_ShouldNotAffectRejectedCounters()
    {
        _transferMetrics.IncrementCompleted();

        _rejectedCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(0);
    }

    [Fact]
    public void IncrementRejected_ShouldNotAffectCompletedCounters()
    {
        _transferMetrics.IncrementRejected();

        _completedCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(0);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();
    }
}
