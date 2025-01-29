using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Internals;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using ProjectOrigin.Vault.Extensions;
using Xunit;

namespace ProjectOrigin.Vault.Tests.Extensions;

public class RandomDelayConfigurationExtensionsTests
{
    [Fact]
    public async Task Should_Apply_RandomDelay_To_Each_Message()
    {
        const int minDelayInMilliseconds = 5;
        const int maxDelayInMilliseconds = 1000;
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<TestConsumer>(consumerCfg =>
                {
                    consumerCfg.UseRandomDelay<TestConsumer, TestMessage>(minDelayInMilliseconds, maxDelayInMilliseconds);
                });

                cfg.UsingInMemory((context, inMemoryQueue) => { inMemoryQueue.ConfigureEndpoints(context); });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            for (var i = 0; i < 5; i++)
            {
                await harness.Bus.Publish(new TestMessage());
            }

            Assert.True(await harness.Consumed.Any<TestMessage>(),
                "Expected the test harness to consume TestMessage, but none was found.");

            var consumerHarness = harness.GetConsumerHarness<TestConsumer>();

            var consumedContexts = await consumerHarness
                .Consumed
                .SelectAsync<TestMessage>()
                .Take(5)
                .ToListAsync();

            Assert.Equal(5, consumedContexts.Count);

            var delays = consumedContexts
                .Select(ctx =>
                    ctx.Context.TryGetPayload<RandomDelayPayload>(out var payload)
                        ? payload.Delay
                        : 0)
                .ToList();

            Assert.Equal(5, delays.Count);

            Assert.All(delays, d => Assert.InRange(d, minDelayInMilliseconds, maxDelayInMilliseconds));

            Assert.Equal(5, delays.Distinct().Count());
        }
        finally
        {
            await harness.Stop();
        }
    }
}

public record TestMessage;

public class TestConsumer : IConsumer<TestMessage>
{
    public Task Consume(ConsumeContext<TestMessage> context)
    {
        return Task.CompletedTask;
    }
}


