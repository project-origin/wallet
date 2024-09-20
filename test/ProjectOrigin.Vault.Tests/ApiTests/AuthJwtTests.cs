using AutoFixture;
using FluentAssertions;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.Vault.Tests;

public class AuthJwtTests : WalletSystemTestsBase, IClassFixture<InMemoryFixture>
{
    private const string EndpointId = "58c092ae-7637-461e-ab7a-5ea972b1295c";
    private const string AggregateParam = "timeAggregate=hour&timeZone=Europe/Copenhagen";

    public AuthJwtTests(
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
            {"auth:jwt:EnableScopeValidation", "true"}
        });
    }

    [Theory]
    [InlineData("v1/wallets")]
    [InlineData($"v1/wallets/{EndpointId}")]
    [InlineData("v1/certificates")]
    [InlineData("v1/claims")]
    [InlineData("v1/transfers")]
    [InlineData($"v1/aggregate-certificates?{AggregateParam}")]
    [InlineData($"v1/aggregate-claims?{AggregateParam}")]
    [InlineData($"v1/aggregate-transfers?{AggregateParam}")]
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
    [InlineData("po:wallets:read", "v1/wallets", HttpStatusCode.OK)]
    [InlineData("po:wallets:read", $"v1/wallets/{EndpointId}", HttpStatusCode.NotFound)]
    [InlineData("po:certificates:read", "v1/certificates", HttpStatusCode.OK)]
    [InlineData("po:certificates:read", $"v1/aggregate-certificates?{AggregateParam}", HttpStatusCode.OK)]
    [InlineData("po:claims:read", "v1/claims", HttpStatusCode.OK)]
    [InlineData("po:claims:read", $"v1/aggregate-claims?{AggregateParam}", HttpStatusCode.OK)]
    [InlineData("po:transfers:read", "v1/transfers", HttpStatusCode.OK)]
    [InlineData("po:transfers:read", $"v1/aggregate-transfers??{AggregateParam}", HttpStatusCode.OK)]
    public async Task Verify_Get_Allowed(string scope, string url, HttpStatusCode expected)
    {
        //Arrange
        var httpClient = CreateAuthenticatedHttpClient(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            scopes: [scope]);

        //Act
        var res = await httpClient.GetAsync(url);

        //Assert
        res.StatusCode.Should().Be(expected);
    }

    [Theory]
    [InlineData("v1/wallets")]
    [InlineData($"v1/wallets/{EndpointId}/endpoints")]
    [InlineData("v1/external-endpoints")]
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
    [InlineData("po:wallets:create", "v1/wallets", HttpStatusCode.Created)]
    [InlineData("po:wallet-endpoints:create", $"v1/wallets/{EndpointId}/endpoints", HttpStatusCode.NotFound)]
    [InlineData("po:external-endpoints:create", "v1/external-endpoints", HttpStatusCode.BadRequest)]
    [InlineData("po:claims:create", "v1/claims", HttpStatusCode.BadRequest)]
    [InlineData("po:transfers:create", "v1/transfers", HttpStatusCode.BadRequest)]
    public async Task Verify_Post_Allowed(string scope, string url, HttpStatusCode expected)
    {
        //Arrange
        var httpClient = CreateAuthenticatedHttpClient(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            scopes: [scope]);

        //Act
        var res = await httpClient.PostAsync(url, JsonContent.Create(new { }));

        //Assert
        res.StatusCode.Should().Be(expected);
    }
}
