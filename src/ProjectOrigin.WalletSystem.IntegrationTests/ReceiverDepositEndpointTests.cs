using System;
using System.Threading.Tasks;
using AutoFixture;
using Dapper;
using FluentAssertions;
using Google.Protobuf;
using Grpc.Core;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.V1;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.WalletSystem.IntegrationTests
{
    public class ReceiverDepositEndpointTests : WalletSystemTestsBase, IClassFixture<InMemoryFixture>
    {
        private Fixture _fixture;

        public ReceiverDepositEndpointTests(
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
            _fixture = new Fixture();
        }

        [Fact]
        public async void CreateReceiverDepositEndpoint()
        {
            var (subject, header) = GenerateUserHeader();

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

            var response = await client.CreateReceiverDepositEndpointAsync(request, header);

            response.Should().NotBeNull();
            response.ReceiverId.Should().NotBeNull();

            using (var connection = _dbFixture.GetConnectionFactory().CreateConnection())
            {
                var foundDepositEndpoint = connection.QuerySingle<DepositEndpoint>("SELECT * FROM DepositEndpoints WHERE Owner = @subject", new { subject });

                request.WalletDepositEndpoint.PublicKey.Should().Equal(foundDepositEndpoint.PublicKey.Export().ToArray());
                request.WalletDepositEndpoint.Endpoint.Should().Be(foundDepositEndpoint.Endpoint);
                request.Reference.Should().Be(foundDepositEndpoint.ReferenceText);
            }
        }

        [Fact]
        public async void CreateReceiverDepositNullIsUnique()

        {
            var reference = _fixture.Create<string>();

            var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);
            var (receiverSubject1, receiverHeader1) = GenerateUserHeader();
            var (receiverSubject2, receiverHeader2) = GenerateUserHeader();

            var key = Algorithm.GenerateNewPrivateKey();
            var publicKey = key.Derive(42).Neuter();
            var request = new CreateReceiverDepositEndpointRequest
            {
                Reference = reference,
                WalletDepositEndpoint = new WalletDepositEndpoint
                {
                    Endpoint = "SomeEndpoint",
                    Version = 1,
                    PublicKey = ByteString.CopyFrom(publicKey.Export())
                }
            };

            await client.CreateReceiverDepositEndpointAsync(request, receiverHeader1);
            await client.CreateReceiverDepositEndpointAsync(request, receiverHeader2);

            using (var connection = _dbFixture.GetConnectionFactory().CreateConnection())
            {
                var foundDepositEndpoints = connection.Query<DepositEndpoint>("SELECT * FROM DepositEndpoints Where ReferenceText = @reference", new { reference });
                foundDepositEndpoints.Should().HaveCount(2);
            }
        }

        [Fact]
        public async void CreateReceiverDepositEndpointOnSameSystem()
        {
            var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);
            var (senderSubject, senderHeader) = GenerateUserHeader();
            var (receiverSubject, receiverHeader) = GenerateUserHeader();

            var createDepositEndpointResponse = await client.CreateWalletDepositEndpointAsync(new CreateWalletDepositEndpointRequest(), receiverHeader);

            var request = new CreateReceiverDepositEndpointRequest
            {
                WalletDepositEndpoint = createDepositEndpointResponse.WalletDepositEndpoint
            };

            var createReceiverResponse = await client.CreateReceiverDepositEndpointAsync(request, senderHeader);

            createReceiverResponse.Should().NotBeNull();
            createReceiverResponse.ReceiverId.Should().NotBeNull();

            using (var connection = _dbFixture.GetConnectionFactory().CreateConnection())
            {
                var foundDepositEndpoints = connection.Query<DepositEndpoint>("SELECT * FROM DepositEndpoints");

                foundDepositEndpoints.Should().HaveCount(2);
            }
        }

        [Fact]
        public async void CreateSelfReferenceNotAllowed()
        {
            var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);
            var (subject, header) = GenerateUserHeader();

            var createDepositEndpointResponse = await client.CreateWalletDepositEndpointAsync(new CreateWalletDepositEndpointRequest(), header);

            var request = new CreateReceiverDepositEndpointRequest
            {
                WalletDepositEndpoint = createDepositEndpointResponse.WalletDepositEndpoint
            };

            Func<Task> sutMethod = async () => await client.CreateReceiverDepositEndpointAsync(request, header);

            await sutMethod.Should().ThrowAsync<RpcException>().WithMessage("""Status(StatusCode="InvalidArgument", Detail="Cannot create receiver deposit endpoint to self.")""");
        }
    }
}
