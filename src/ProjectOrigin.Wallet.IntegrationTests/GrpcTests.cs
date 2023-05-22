using ProjectOrigin.Wallet.IntegrationTests.TestClassFixtures;
using ProjectOrigin.Wallet.Server;
using ProjectOrigin.Wallet.V1;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.Wallet.IntegrationTests;

public class GrpcTests : IClassFixture<GrpcTestFixture<Startup>>, IClassFixture<PostgresDatabaseFixture>
{
    private GrpcTestFixture<Startup> _grpcFixture;
    private PostgresDatabaseFixture _dbFixture;

    public GrpcTests(GrpcTestFixture<Startup> grpcFixture, PostgresDatabaseFixture dbFixture)
    {
        this._grpcFixture = grpcFixture;
        this._dbFixture = dbFixture;

        grpcFixture.ConfigureHostConfiguration(new Dictionary<string, string?>()
            {
                {"ConnectionStrings:Database", dbFixture.ConnectionString}
            });
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
