using Dapper;
using FluentAssertions;
using Grpc.Core;
using ProjectOrigin.Wallet.IntegrationTests.TestClassFixtures;
using ProjectOrigin.Wallet.Server;
using ProjectOrigin.Wallet.Server.Database;
using ProjectOrigin.Wallet.Server.Models;
using ProjectOrigin.Wallet.V1;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.Wallet.IntegrationTests;

public class GrpcTests : GrpcTestsBase
{
    private JwtGenerator _tokenGenerator;

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

        var headers = new Metadata();
        headers.Add("Authorization", $"Bearer {token}");

        var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);
        var request = new CreateWalletSectionRequest();

        // Act
        var walletSection = await client.CreateWalletSectionAsync(request, headers);

        // Assert
        Assert.NotNull(walletSection);
        walletSection.Version.Should().Be(1);
        walletSection.Endpoint.Should().Be(endpoint);
        walletSection.SectionPublicKey.Should().NotBeNullOrEmpty();

        using (var connection = new DbConnectionFactory(_dbFixture.ConnectionString).CreateConnection())
        {
            var foundSection = connection.QuerySingle<WalletSection>("SELECT * FROM WalletSections");
            Assert.True(Enumerable.SequenceEqual(foundSection.PublicKey.Export().ToArray(), walletSection.SectionPublicKey));

            var foundWallet = connection.QuerySingle<OwnerWallet>("SELECT * FROM Wallets where owner = @owner", new { owner = subject });
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
