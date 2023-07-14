using AutoFixture;
using Dapper;
using FluentAssertions;
using Google.Protobuf;
using Grpc.Core;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.V1;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.WalletSystem.IntegrationTests
{
    public class ReceiverDepositEndpointTests : WalletSystemTestsBase
    {
        private Fixture _fixture;

        public ReceiverDepositEndpointTests(GrpcTestFixture<Startup> grpcFixture, PostgresDatabaseFixture dbFixture, ITestOutputHelper outputHelper)
            : base(grpcFixture, dbFixture, outputHelper, null)
        {
            _fixture = new Fixture();
        }

        [Fact]
        public async void CreateReceiverDepositEndpoint()
        {
            var owner = _fixture.Create<string>();

            var someOwnerName = _fixture.Create<string>();
            var token = _tokenGenerator.GenerateToken(owner, someOwnerName);
            var headers = new Metadata();
            headers.Add("Authorization", $"Bearer {token}");

            var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);

            var key = Algorithm.GenerateNewPrivateKey();
            var publicKey = key.Derive(42).Neuter();
            var request = new CreateReceiverDepositEndpointRequest
            {
                Reference = "SomeRef",
                WalletDepositEndpoint = new WalletDepositEndpoint
                {
                    Endpoint = "SomeEndpoint",
                    Version = 1,
                    PublicKey = ByteString.CopyFrom(publicKey.Export())
                }
            };

            var response = await client.CreateReceiverDepositEndpointAsync(request, headers);

            response.Should().NotBeNull();
            response.ReceiverId.Should().NotBeNull();

            using (var connection = new DbConnectionFactory(_dbFixture.ConnectionString).CreateConnection())
            {
                var foundDepositEndpoint = connection.QuerySingle<DepositEndpoint>("SELECT * FROM DepositEndpoints");

                request.WalletDepositEndpoint.PublicKey.Should().Equal(foundDepositEndpoint.PublicKey.Export().ToArray());
            }
        }
    }
}
