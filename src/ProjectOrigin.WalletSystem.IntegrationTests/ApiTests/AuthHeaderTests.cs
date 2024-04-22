using AutoFixture;
using FluentAssertions;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class AuthHeaderTests : WalletSystemTestsBase, IClassFixture<InMemoryFixture>
{
    private const string HeaderName = "SomeHeader";

    public AuthHeaderTests(
        TestServerFixture<Startup> serverFixture,
        PostgresDatabaseFixture dbFixture,
        InMemoryFixture inMemoryFixture,
        JwtTokenIssuerFixture jwtTokenIssuerFixture,
        ITestOutputHelper outputHelper)
        : base(
              serverFixture,
              dbFixture,
              inMemoryFixture,
              jwtTokenIssuerFixture,
              outputHelper,
              null)
    {
        serverFixture.ConfigureHostConfiguration(new()
        {
            {"auth:type", "header"},
            {"auth:header:headerName", HeaderName}
        });
    }

    [Theory]
    [InlineData("v1/wallets")]
    [InlineData("v1/certificates")]
    [InlineData("v1/claims")]
    [InlineData("v1/transfers")]
    public async Task Verify_Get_Forbidden(string url)
    {
        //Arrange
        var httpClient = _serverFixture.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Add(HeaderName, _fixture.Create<string>());

        //Act
        var res = await httpClient.GetAsync(url);

        //Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
