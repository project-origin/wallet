using System.Collections.Generic;

namespace ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;

public class InMemoryFixture : IMessageBrokerFixture
{
    public Dictionary<string, string?> Configuration => new Dictionary<string, string?>()
        {
            {"MessageBroker:Type", "InMemory"},
        };
}
