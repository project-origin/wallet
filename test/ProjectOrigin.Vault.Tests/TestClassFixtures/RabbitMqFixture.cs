using System.Collections.Generic;
using System.Threading.Tasks;
using Testcontainers.RabbitMq;
using Xunit;

namespace ProjectOrigin.Vault.Tests.TestClassFixtures;

public class RabbitMqFixture : IMessageBrokerFixture, IAsyncLifetime
{
    private const string Username = "rabbitmq";
    private const string Password = "rabbitmq";
    private const int RabbitMqPort = 5672;
    private readonly RabbitMqContainer _rabbitMqContainer;

    public RabbitMqFixture()
    {
        _rabbitMqContainer = new RabbitMqBuilder()
            .WithUsername(Username)
            .WithPassword(Password)
            .Build();
    }

    public Dictionary<string, string?> Configuration => new Dictionary<string, string?>()
        {
            {"MessageBroker:Type", "RabbitMq"},
            {"MessageBroker:RabbitMq:Host", _rabbitMqContainer.Hostname},
            {"MessageBroker:RabbitMq:Port", _rabbitMqContainer.GetMappedPublicPort(RabbitMqPort).ToString()},
            {"MessageBroker:RabbitMq:Username", Username},
            {"MessageBroker:RabbitMq:Password", Password},
        };

    public async Task InitializeAsync() => await _rabbitMqContainer.StartAsync().ConfigureAwait(false);
    public async Task DisposeAsync() => await _rabbitMqContainer.StopAsync().ConfigureAwait(false);
}
