using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Configuration;

namespace ProjectOrigin.Vault.Extensions;

public static class RandomDelayConfigurationExtensions
{
    public static void UseRandomDelay<TConsumer, TMessage>(this IConsumerConfigurator<TConsumer> configurator, int minDelayInMilliseconds = 1,
        int maxDelayInMilliseconds = 1500)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        configurator.AddPipeSpecification(new RandomDelaySpecification<TConsumer, TMessage>(minDelayInMilliseconds, maxDelayInMilliseconds));
    }
}

public class RandomDelaySpecification<TConsumer, TMessage>(int minDelayInMilliseconds, int maxDelayInMilliseconds)
    : IPipeSpecification<ConsumerConsumeContext<TConsumer>>
    where TConsumer : class, IConsumer<TMessage>
    where TMessage : class
{
    public void Apply(IPipeBuilder<ConsumerConsumeContext<TConsumer>> builder)
    {
        builder.AddFilter(new RandomDelayFilter<TConsumer>(minDelayInMilliseconds, maxDelayInMilliseconds));
    }

    public IEnumerable<ValidationResult> Validate()
    {
        if (minDelayInMilliseconds < 0)
            yield return this.Failure("RandomDelayFilter", "minDelayInMilliseconds cannot be negative");

        if (maxDelayInMilliseconds <= minDelayInMilliseconds)
            yield return this.Failure("RandomDelayFilter",
                "maxDelayInMilliseconds must be greater than minDelayInMilliseconds");
    }
}

public class RandomDelayFilter<TConsumer>(int minDelayInMilliseconds, int maxDelayInMilliseconds) : IFilter<ConsumerConsumeContext<TConsumer>>
    where TConsumer : class
{
    public async Task Send(ConsumerConsumeContext<TConsumer> context,
        IPipe<ConsumerConsumeContext<TConsumer>> next)
    {
        var delay = Random.Shared.Next(minDelayInMilliseconds, maxDelayInMilliseconds + 1);

        context.GetOrAddPayload(() => new RandomDelayPayload { Delay = delay });

        await Task.Delay(delay);
        await next.Send(context);
    }

    public void Probe(ProbeContext context)
    {
        var scope = context.CreateFilterScope("random-delay");
        scope.Add("description", $"Adds a random delay between {minDelayInMilliseconds}-{maxDelayInMilliseconds} ms to each message.");
    }
}

public class RandomDelayPayload
{
    public int Delay { get; set; }
}
