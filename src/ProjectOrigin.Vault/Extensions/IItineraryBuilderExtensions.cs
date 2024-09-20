using System;
using MassTransit;

namespace ProjectOrigin.Vault.Extensions;

public static class IItineraryBuilderExtensions
{
    public static void AddActivity<T, TArguments>(this IItineraryBuilder builder, IEndpointNameFormatter formatter, TArguments arguments)
        where T : class, IExecuteActivity<TArguments>
        where TArguments : class
    {
        var uri = new Uri($"exchange:{formatter.ExecuteActivity<T, TArguments>()}");
        builder.AddActivity(typeof(T).Name, uri, arguments);
    }
}
