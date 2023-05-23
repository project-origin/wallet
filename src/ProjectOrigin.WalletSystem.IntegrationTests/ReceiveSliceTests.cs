using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System;
using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.V1;
using Xunit;
using Google.Protobuf;
using ProjectOrigin.WalletSystem.Server.HDWallet;
using Npgsql;
using Dapper;
using ProjectOrigin.WalletSystem.Server.Repositories;
using ProjectOrigin.WalletSystem.Server.Models;
using Xunit.Abstractions;

namespace ProjectOrigin.WalletSystem.IntegrationTests
{
    public class ReceiveSliceTests : GrpcTestsBase
    {
        const string RegistryName = "RegistryA";
        public ReceiveSliceTests(GrpcTestFixture<Startup> grpcFixture, PostgresDatabaseFixture dbFixture, ITestOutputHelper outputHelper)
            : base(grpcFixture, dbFixture, outputHelper)
        {
        }

        private async Task<WalletSection> CreateWalletSection(string owner)
        {
            using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
            {
                var walletRepository = new WalletRepository(connection);
                var wallet = new Wallet(Guid.NewGuid(), owner, Algorithm.GenerateNewPrivateKey());
                await walletRepository.Create(wallet);

                var section = new WalletSection(Guid.NewGuid(), wallet.Id, 1, wallet.PrivateKey.Derive(1).PublicKey);
                await walletRepository.CreateSection(section);

                return section;
            }
        }

        [Fact]
        public async Task ReceiveSlice()
        {
            //Arrange
            var certId = Guid.NewGuid();
            var owner = "John";
            var section = await CreateWalletSection(owner);
            var client = new ReceiveSliceService.ReceiveSliceServiceClient(_grpcFixture.Channel);
            var request = new ReceiveRequest()
            {
                CertificateId = new Register.V1.FederatedStreamId()
                {
                    Registry = RegistryName,
                    StreamId = new Register.V1.Uuid() { Value = certId.ToString() },
                },
                WalletSectionPublicKey = ByteString.CopyFrom(section.PublicKey.Export()),
                WalletSectionPosition = 2,
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
