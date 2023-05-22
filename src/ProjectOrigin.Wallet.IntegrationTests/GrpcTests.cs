using ProjectOrigin.Wallet.IntegrationTests.TestClassFixtures;
using ProjectOrigin.Wallet.Server;
using ProjectOrigin.Wallet.V1;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.Wallet.IntegrationTests;

public class GrpcTests : GrpcTestsBase
{
    public GrpcTests(GrpcTestFixture<Startup> grpcFixture, PostgresDatabaseFixture dbFixture, ITestOutputHelper outputHelper) : base(grpcFixture, dbFixture, outputHelper)
    {
    }

    [Fact]
    public async Task CreateWalletSection_NonEmptyResponse()
    {
        var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);

        var createRequest = new CreateWalletSectionRequest();
        var walletSection = await client.CreateWalletSectionAsync(createRequest);

        Assert.NotNull(walletSection);
    }
}
