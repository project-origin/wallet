using ProjectOrigin.Wallet.IntegrationTests.TestClassFixtures;
using ProjectOrigin.Wallet.Server;
using ProjectOrigin.Wallet.V1;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.Wallet.IntegrationTests;

public class GrpcTests : IClassFixture<GrpcTestFixture<Startup>>, IClassFixture<PostgresDatabaseFixture>
{
    private GrpcTestFixture<Startup> grpcFixture;

    public GrpcTests(GrpcTestFixture<Startup> grpcFixture)
    {
        this.grpcFixture = grpcFixture;
    }

    [Fact]
    public async Task CreateWalletSection_NonEmptyResponse()
    {
        var client = new WalletService.WalletServiceClient(grpcFixture.Channel);

        var createRequest = new CreateWalletSectionRequest();
        var walletSection = await client.CreateWalletSectionAsync(createRequest);

        Assert.NotNull(walletSection);
    }
}
