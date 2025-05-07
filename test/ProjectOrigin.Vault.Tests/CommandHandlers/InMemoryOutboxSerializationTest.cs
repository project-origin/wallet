using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ProjectOrigin.Vault.Tests.CommandHandlers;

public class InMemoryOutboxSerializationTest
{
    public record TestMessage(string Value);

    [Fact]
    public async Task Should_Serialize_When_Using_InMemoryOutbox()
    {
        var concurrentExecutions = new ConcurrentBag<DateTime>();

        var services = new ServiceCollection();

        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<TestConsumer>(cfg =>
            {
                cfg.UseInMemoryOutbox(); // Try removing this to see concurrent behavior
            });

            x.UsingInMemory((context, cfg) =>
            {
                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddSingleton(concurrentExecutions);

        await using var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();

        var bus = provider.GetRequiredService<IBus>();

        // Send multiple messages "at once"
        await Task.WhenAll(
            bus.Publish(new TestMessage("A")),
            bus.Publish(new TestMessage("B")),
            bus.Publish(new TestMessage("C"))
        );

        await Task.Delay(2000); // give consumers time to finish

        // If consumers are serialized, timestamps will be 500ms apart
        var timestamps = concurrentExecutions.OrderBy(x => x).ToList();

        Assert.True(timestamps.Count == 3, "All 3 messages should be handled");

        var diff1 = timestamps[1] - timestamps[0];
        var diff2 = timestamps[2] - timestamps[1];

        // If serialized, both diffs will be close to 500ms
        Assert.True(diff1.TotalMilliseconds > 400, $"Expected serialized processing, got diff1={diff1.TotalMilliseconds}ms");
        Assert.True(diff2.TotalMilliseconds > 400, $"Expected serialized processing, got diff2={diff2.TotalMilliseconds}ms");
    }


}

public class TestConsumer : IConsumer<InMemoryOutboxSerializationTest.TestMessage>
{
    private readonly ConcurrentBag<DateTime> _executions;

    public TestConsumer(ConcurrentBag<DateTime> executions)
    {
        _executions = executions;
    }

    public async Task Consume(ConsumeContext<InMemoryOutboxSerializationTest.TestMessage> context)
    {
        _executions.Add(DateTime.UtcNow);
        await Task.Delay(500); // simulate long processing
    }
}
