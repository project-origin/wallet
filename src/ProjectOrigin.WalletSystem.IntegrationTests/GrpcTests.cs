using FluentAssertions;
using Grpc.Core;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using ProjectOrigin.WalletSystem.Server.Repositories;
using ProjectOrigin.WalletSystem.V1;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class GrpcTests : WalletSystemTestsBase, IClassFixture<InMemoryFixture>
{
    public GrpcTests(
        GrpcTestFixture<Startup> grpcFixture,
        PostgresDatabaseFixture dbFixture,
        InMemoryFixture memoryFixture,
        ITestOutputHelper outputHelper)
        : base(
            grpcFixture,
            dbFixture,
            memoryFixture,
            outputHelper,
            null)
    {
    }

    [Fact]
    public async Task can_create_external_endpoint_when_authenticated()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var token = _tokenGenerator.GenerateToken(subject, "John Doe");

        var headers = new Metadata { { "Authorization", $"Bearer {token}" } };

        var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);
        var request = new CreateWalletDepositEndpointRequest();

        // Act
        var externalEndpoint = await client.CreateWalletDepositEndpointAsync(request, headers);

        // Assert
        externalEndpoint.Should().NotBeNull();
        externalEndpoint.WalletDepositEndpoint.Version.Should().Be(1);
        externalEndpoint.WalletDepositEndpoint.Endpoint.Should().Be(endpoint);
        externalEndpoint.WalletDepositEndpoint.PublicKey.Should().NotBeNullOrEmpty();


        using (var connection = _dbFixture.GetConnectionFactory().CreateConnection())
        {
            var walletRepository = new WalletRepository(connection);

            var publicKey = Algorithm.ImportHDPublicKey(externalEndpoint.WalletDepositEndpoint.PublicKey.Span);
            var endpoint = await walletRepository.GetWalletEndpoint(publicKey);

            endpoint.Should().NotBeNull();

            var wallet = await walletRepository.GetWallet(endpoint!.WalletId);
            wallet.Should().NotBeNull();
            wallet.Owner.Should().Be(subject);
        }
    }

    [Fact]
    public async Task throw_unauthenticated_when_no_jwt()
    {
        // Arrange
        var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);
        var request = new CreateWalletDepositEndpointRequest();

        // Act
        Func<Task> sutMethod = async () => await client.CreateWalletDepositEndpointAsync(request);

        // Assert
        await sutMethod.Should().ThrowAsync<RpcException>().WithMessage("Status(StatusCode=\"Unauthenticated\", Detail=\"Bad gRPC response. HTTP status code: 401\")");
    }
}
