using AutoFixture;
using FluentAssertions;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class AuthTests : WalletSystemTestsBase, IClassFixture<InMemoryFixture>
{
    public AuthTests(
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
        serverFixture.ConfigureHostConfiguration(new(){
            {"Jwt:EnableScopeValidation", "true"}
        });
    }

    [Theory]
    [InlineData("v1/certificates")]
    [InlineData("v1/aggregate-certificates?timeAggregate=hour&timeZone=Europe/Copenhagen")]
    [InlineData("v1/claims")]
    [InlineData("v1/aggregate-claims?timeAggregate=hour&timeZone=Europe/Copenhagen")]
    [InlineData("v1/transfers")]
    [InlineData("v1/aggregate-transfers?timeAggregate=hour&timeZone=Europe/Copenhagen")]
    public async Task Verify_Get_Forbidden(string url)
    {
        //Arrange
        var httpClient = CreateAuthenticatedHttpClient(
            _fixture.Create<string>(),
            _fixture.Create<string>());

        //Act
        var res = await httpClient.GetAsync(url);

        //Assert
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("certificate:read", "v1/certificates")]
    [InlineData("certificate:read", "v1/aggregate-certificates?timeAggregate=hour&timeZone=Europe/Copenhagen")]
    [InlineData("claim:read", "v1/claims")]
    [InlineData("claim:read", "v1/aggregate-claims?timeAggregate=hour&timeZone=Europe/Copenhagen")]
    [InlineData("transfer:read", "v1/transfers")]
    [InlineData("transfer:read", "v1/aggregate-transfers?timeAggregate=hour&timeZone=Europe/Copenhagen")]
    public async Task Verify_Get_Allowed(string scope, string url)
    {
        //Arrange
        var httpClient = CreateAuthenticatedHttpClient(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            scopes: [scope]);

        //Act
        var res = await httpClient.GetAsync(url);

        //Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("v1/claims")]
    [InlineData("v1/transfers")]
    public async Task Verify_Post_Forbidden(string url)
    {
        //Arrange
        var httpClient = CreateAuthenticatedHttpClient(
            _fixture.Create<string>(),
            _fixture.Create<string>());

        //Act
        var res = await httpClient.PostAsync(url, JsonContent.Create(new { }));

        //Assert
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("claim:create", "v1/claims")]
    [InlineData("transfer:create", "v1/transfers")]
    /// <summary>
    /// Bad request equals that call was allowed but the request was not valid.
    /// </summary>
    public async Task Verify_Post_Allowed(string scope, string url)
    {
        //Arrange
        var httpClient = CreateAuthenticatedHttpClient(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            scopes: [scope]);

        //Act
        var res = await httpClient.PostAsync(url, JsonContent.Create(new { }));

        //Assert
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
