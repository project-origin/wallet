using ProjectOrigin.Wallet.IntegrationTests.TestClassFixtures;
using ProjectOrigin.Wallet.Server;
using System;
using System.Threading.Tasks;
using Grpc.Core;
using ProjectOrigin.Wallet.V1;
using Xunit;

namespace ProjectOrigin.Wallet.IntegrationTests
{
    public class ReceiveSliceTests : GrpcTestsBase
    {
        public ReceiveSliceTests(GrpcTestFixture<Startup> grpcFixture, PostgresDatabaseFixture dbFixture)
            : base(grpcFixture, dbFixture)
        {
        }

        [Fact]
        public async Task ReceiveSlice()
        {
            //Arrange
            var subject = Guid.NewGuid().ToString();
            var token = _tokenGenerator.GenerateToken(subject, "Some user");

            var headers = new Metadata();
            headers.Add("Authorization", $"Bearer {token}");

            var client = new ExternalWalletService.ExternalWalletServiceClient(_grpcFixture.Channel);
            var request = new ReceiveRequest();

            //Act
            var response = await client.ReceiveSliceAsync(request, headers);

            //Assert
            Assert.NotNull(response);
        }
    }
}
