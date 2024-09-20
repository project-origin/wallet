using System.Collections.Generic;

namespace ProjectOrigin.Vault.Tests.TestClassFixtures;

public interface IMessageBrokerFixture
{
    Dictionary<string, string?> Configuration { get; }
}
