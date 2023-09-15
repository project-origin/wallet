using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.WalletSystem.IntegrationTests.MessageBroker;

public class RabbitMqTests : WalletSystemTestsBase, IClassFixture<RabbitMqFixture>
{
    public RabbitMqTests(
        GrpcTestFixture<Startup> grpcFixture,
        PostgresDatabaseFixture dbFixture,
        RabbitMqFixture messageBrokerFixture,
        ITestOutputHelper outputHelper) : base(
            grpcFixture,
            dbFixture,
            messageBrokerFixture,
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
