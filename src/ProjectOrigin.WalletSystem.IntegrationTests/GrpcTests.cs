using Dapper;
using FluentAssertions;
using Grpc.Core;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.V1;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class GrpcTests : WalletSystemTestsBase
{
    public GrpcTests(GrpcTestFixture<Startup> grpcFixture, PostgresDatabaseFixture dbFixture, ITestOutputHelper outputHelper)
        : base(grpcFixture, dbFixture, outputHelper, null)
    {
    }

    [Fact]
    public async Task can_create_wallet_section_when_authenticated()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var token = _tokenGenerator.GenerateToken(subject, "John Doe");

        var headers = new Metadata { { "Authorization", $"Bearer {token}" } };

        var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);
        var request = new CreateWalletDepositEndpointRequest();

        // Act
        var walletSection = await client.CreateWalletDepositEndpointAsync(request, headers);

        // Assert
        walletSection.Should().NotBeNull();
        walletSection.WalletDepositEndpoint.Version.Should().Be(1);
        walletSection.WalletDepositEndpoint.Endpoint.Should().Be(endpoint);
        walletSection.WalletDepositEndpoint.PublicKey.Should().NotBeNullOrEmpty();

        using (var connection = new DbConnectionFactory(_dbFixture.ConnectionString).CreateConnection())
        {
            var foundSection = connection.QuerySingle<WalletSection>("SELECT * FROM WalletSections");

            walletSection.WalletDepositEndpoint.PublicKey.Should().Equal(foundSection.PublicKey.Export().ToArray());

            var foundWallet = connection.QuerySingle<Wallet>("SELECT * FROM Wallets where owner = @owner", new { owner = subject });
            // Wallet should be implicitly created
            foundWallet.Should().NotBeNull();
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
