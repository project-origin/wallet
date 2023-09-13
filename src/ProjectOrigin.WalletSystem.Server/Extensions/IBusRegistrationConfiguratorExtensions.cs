using System;
using MassTransit;
using ProjectOrigin.WalletSystem.Server.Options;
using ProjectOrigin.WalletSystem.Server.Serialization;

public static class IBusRegistrationConfiguratorExtensions
{
    public static void ConfigureMassTransitTransport(this IBusRegistrationConfigurator busConfig, MessageBrokerOptions options)
    {
        switch (options.Type)
        {
            case MessageBrokerType.InMemory:
                Serilog.Log.Logger.Warning("MessageBroker.Type is set to InMemory, this is not recommended for production use, messages are not durable");

                busConfig.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureDefaults(context);
                });
                break;

            case MessageBrokerType.RabbitMq:
                busConfig.UsingRabbitMq((context, cfg) =>
                {
                    cfg.ConfigureDefaults(context);

                    var rabbitOption = options.RabbitMq!;
                    cfg.Host(rabbitOption.Host, rabbitOption.Port, "/", h =>
                    {
                        h.Username(rabbitOption.Username);
                        h.Password(rabbitOption.Password);
                    });
                });
                break;

            default:
                throw new NotSupportedException($"Message broker type ”{options.Type}” not supported");
        }

    }

    private static void ConfigureDefaults<T>(this IBusFactoryConfigurator<T> cfg, IBusRegistrationContext context) where T : IReceiveEndpointConfigurator
    {
        cfg.ConfigureEndpoints(context);

        cfg.ConfigureJsonSerializerOptions(options =>
        {
            options.Converters.Add(new TransactionConverter());
            return options;
        });
    }
}
