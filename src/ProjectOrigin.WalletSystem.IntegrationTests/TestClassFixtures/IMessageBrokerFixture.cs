using System.Collections.Generic;

namespace ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;

public interface IMessageBrokerFixture
{
    Dictionary<string, string?> Configuration { get; }
}
