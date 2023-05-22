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

public class GrpcTests : GrpcTestsBase
{
    private readonly JwtGenerator _tokenGenerator;

    public GrpcTests(GrpcTestFixture<Startup> grpcFixture, PostgresDatabaseFixture dbFixture, ITestOutputHelper outputHelper) : base(grpcFixture, dbFixture, outputHelper)
    {
        _tokenGenerator = new JwtGenerator();
    }

    [Fact]
    public async Task CreateWalletSection_ValidResponse_InDatabase()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var token = _tokenGenerator.GenerateToken(subject, "John Doe");

        var headers = new Metadata { { "Authorization", $"Bearer {token}" } };

        var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);
        var request = new CreateWalletSectionRequest();

        // Act
        var walletSection = await client.CreateWalletSectionAsync(request, headers);

        // Assert
        walletSection.Should().NotBeNull();
        walletSection.Version.Should().Be(1);
        walletSection.Endpoint.Should().Be(endpoint);
        walletSection.SectionPublicKey.Should().NotBeNullOrEmpty();

        using (var connection = new DbConnectionFactory(_dbFixture.ConnectionString).CreateConnection())
        {
            var foundSection = connection.QuerySingle<WalletSection>("SELECT * FROM WalletSections");

            walletSection.SectionPublicKey.Should().Equal(foundSection.PublicKey.Export().ToArray());

            var foundWallet = connection.QuerySingle<Wallet>("SELECT * FROM Wallets where owner = @owner", new { owner = subject });
            // Wallet should be implicitly created
            foundWallet.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task CreateWalletSection_InvalidRequest_Unauthenticated()
    {
        // Arrange
        var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);
        var request = new CreateWalletSectionRequest();

        // Act
        Func<Task> sutMethod = async () => await client.CreateWalletSectionAsync(request);

        // Assert
        await sutMethod.Should().ThrowAsync<RpcException>().WithMessage("Status(StatusCode=\"Unauthenticated\", Detail=\"Bad gRPC response. HTTP status code: 401\")");
    }

}
