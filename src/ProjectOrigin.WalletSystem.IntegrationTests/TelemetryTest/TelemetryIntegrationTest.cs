using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.TelemetryTest;

public class TelemetryIntegrationTest
{
    [Fact]
    public async Task MetricsAreExportedOnActivityTest()
    {
        var activitySource = new ActivitySource("TestActivitySource");
        var mockCollector = Substitute.For<BaseProcessor<Activity>>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("TestActivitySource")
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(mockCollector)
            .Build();

        var exportedItems = new List<Activity>();

        mockCollector.When(x => x.OnEnd(Arg.Any<Activity>()))
            .Do(callInfo => exportedItems.Add(callInfo.Arg<Activity>()));

        using var activity = activitySource.StartActivity("TestActivity");
        activity?.SetTag("key", "value");
        activity?.Stop();

        await Task.Delay(1000);

        exportedItems.Should().HaveCount(1);
        var exportedActivity = exportedItems[0];
        exportedActivity.OperationName.Should().Be("TestActivity");

        bool hasExpectedTag = false;
        foreach (var tag in exportedActivity.Tags)
        {
            if (tag.Key == "key" && tag.Value is string value && value == "value")
            {
                hasExpectedTag = true;
                break;
            }
        }
        hasExpectedTag.Should().BeTrue();
    }
}
