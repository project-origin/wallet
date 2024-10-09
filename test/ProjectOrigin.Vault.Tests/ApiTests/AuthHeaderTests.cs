using AutoFixture;
using FluentAssertions;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.Vault.Tests;

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

    [Fact]
    public async Task Verify_Get()
    {
        //Arrange
        using var httpClient = _serverFixture.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Add(HeaderName, _fixture.Create<string>());

        //Act
        var res = await httpClient.GetAsync("v1/wallets");

        //Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]

    public async Task Verify_Get_Forbidden()
    {
        //Arrange
        using var httpClient = _serverFixture.CreateHttpClient();

        //Act
        var res = await httpClient.GetAsync("v1/wallets");

        //Assert
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
