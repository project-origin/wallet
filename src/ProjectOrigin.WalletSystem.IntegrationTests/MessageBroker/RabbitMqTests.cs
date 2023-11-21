using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.WalletSystem.IntegrationTests.MessageBroker;

public class RabbitMqTests : WalletSystemTestsBase, IClassFixture<RabbitMqFixture>
{
    public RabbitMqTests(
        TestServerFixture<Startup> serverFixture,
        PostgresDatabaseFixture dbFixture,
        RabbitMqFixture messageBrokerFixture,
        JwtTokenIssuerFixture jwtTokenIssuerFixture,
        ITestOutputHelper outputHelper) : base(
            serverFixture,
            dbFixture,
            messageBrokerFixture,
            jwtTokenIssuerFixture,
            outputHelper,
            null)
    {
    }

    [Fact]
    public void VerifyHostStarts()
    {
        // TODO: write test with test-harness
        Assert.True(true);
    }
}
