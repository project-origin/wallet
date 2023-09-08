using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System;
using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.V1;
using Xunit;
using Google.Protobuf;
using Npgsql;
using Dapper;
using ProjectOrigin.WalletSystem.Server.Models;
using Xunit.Abstractions;
using AutoFixture;

namespace ProjectOrigin.WalletSystem.IntegrationTests
{
    public class ReceiveSliceTests : WalletSystemTestsBase, IClassFixture<InMemoryFixture>
    {
        public ReceiveSliceTests(
            GrpcTestFixture<Startup> grpcFixture,
            PostgresDatabaseFixture dbFixture,
            InMemoryFixture inMemoryFixture,
            ITestOutputHelper outputHelper)
            : base(
                  grpcFixture,
                  dbFixture,
                  inMemoryFixture,
                  outputHelper,
                  null)
        {
        }

        [Fact]
        public async Task ReceiveSlice()
        {
            //Arrange
            var certId = Guid.NewGuid();
            var owner = "John";
            var registryName = new Fixture().Create<string>();
            var depositEndpoint = await CreateWalletDepositEndpoint(owner);
            var client = new ReceiveSliceService.ReceiveSliceServiceClient(_grpcFixture.Channel);
            var request = new ReceiveRequest()
            {
                CertificateId = new Common.V1.FederatedStreamId()
                {
                    Registry = registryName,
                    StreamId = new Common.V1.Uuid() { Value = certId.ToString() },
                },
                WalletDepositEndpointPublicKey = ByteString.CopyFrom(depositEndpoint.PublicKey.Export()),
                WalletDepositEndpointPosition = 2,
                Quantity = 240,
                RandomR = ByteString.CopyFrom(new byte[] { 0x01, 0x02, 0x03, 0x04 }),
            };

            //Act
            var response = await client.ReceiveSliceAsync(request);

            //Assert
            using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
            {
                // Verify slice created in database
                var slice = await connection.QueryFirstOrDefaultAsync<ReceivedSlice>("SELECT * FROM ReceivedSlices WHERE certificateId = @id", new { id = certId });
                Assert.NotNull(slice);
            }
        }
    }
}
