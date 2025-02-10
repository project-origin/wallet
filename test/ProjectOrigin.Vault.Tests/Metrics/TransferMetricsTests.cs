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

    private readonly MetricCollector<long> _transferIntentsCollector;
    private readonly MetricCollector<long> _transfersCompletedCollector;
    private readonly MetricCollector<long> _transfersFailedCollector;


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

        _transferIntentsCollector =
            new MetricCollector<long>(meterFactory, "ProjectOrigin.Vault", "po_vault_transfer_intent_count");
        _transfersCompletedCollector =
            new MetricCollector<long>(meterFactory, "ProjectOrigin.Vault", "po_vault_transfer_completed_count");

        _transfersFailedCollector = new MetricCollector<long>(meterFactory, "ProjectOrigin.Vault", "po_vault_transfer_failed_count");
    }

    [Fact]
    public void IncrementCompleted_ShouldUpdateCounter()
    {
        _transferMetrics.IncrementCompleted();

        _transfersCompletedCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(1);
    }

    [Fact]
    public void IncrementIntents_ShouldUpdateCounter()
    {
        _transferMetrics.IncrementTransferIntents();

        _transferIntentsCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(1);
    }

    [Fact]
    public void IncrementCompleted_ShouldNotAffectRejectedCounter()
    {
        _transferMetrics.IncrementCompleted();

        _transferIntentsCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(0);
    }

    [Fact]
    public void IncrementIntents_ShouldNotAffectCompletedCounter()
    {
        _transferMetrics.IncrementTransferIntents();

        _transfersCompletedCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(0);
    }

    [Fact]
    public void IncrementFailedTransfers_ShouldUpdateCounter()
    {
        _transferMetrics.IncrementFailedTransfers();
        _transfersFailedCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(1);
    }

    [Fact]
    public void IncrementFailedClaims_ShouldNotAffectOtherCounters()
    {
        _transferMetrics.IncrementFailedTransfers();
        _transferIntentsCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(0);
        _transfersCompletedCollector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(0);
    }
    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();
    }
}
