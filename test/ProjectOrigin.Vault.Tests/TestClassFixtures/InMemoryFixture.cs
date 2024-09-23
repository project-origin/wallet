using System.Collections.Generic;

namespace ProjectOrigin.Vault.Tests.TestClassFixtures;

public class InMemoryFixture : IMessageBrokerFixture
{
    public Dictionary<string, string?> Configuration => new Dictionary<string, string?>()
        {
            {"MessageBroker:Type", "InMemory"},
        };
}
