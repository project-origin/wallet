using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using ProjectOrigin.Vault.Services.REST.v1;
using ProjectOrigin.Vault.Tests.TestExtensions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.Vault.Tests;

public class ApiBasePathTests : WalletSystemTestsBase, IClassFixture<InMemoryFixture>
{
    private readonly PathString _basePath = "/foo-bar-baz";

    public ApiBasePathTests(
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
            {"ServiceOptions:PathBase", _basePath},
        });
    }

    [Fact]
    public async Task open_api_specification_paths_starts_with_base_path()
    {
        var httpClient = _serverFixture.CreateHttpClient();
        var specificationResponse = await httpClient.GetAsync("swagger/v1/swagger.json");
        var specification = await specificationResponse.Content.ReadAsStringAsync();
        specification.Should().Contain($"{_basePath}/v1/certificates");
    }

    [Fact]
    public async Task api_returns_ok_when_base_path_is_correct()
    {
        var subject = _fixture.Create<string>();
        await _dbFixture.CreateWallet(subject);
        var httpClient = CreateAuthenticatedHttpClient(subject, _fixture.Create<string>());

        var result = await httpClient.GetAsync($"{_basePath}/v1/certificates");

        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task api_returns_not_found_when_base_path_is_incorrect()
    {
        var httpClient = CreateAuthenticatedHttpClient(_fixture.Create<string>(), _fixture.Create<string>());

        PathString wrongBasePath = "/api";

        var result = await httpClient.GetAsync($"{wrongBasePath}/v1/certificates");

        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SwaggerJson_ContainsWalletTag()
    {
        var httpClient = _serverFixture.CreateHttpClient();
        var swaggerJsonUrl = "swagger/v1/swagger.json";
        var swaggerResponse = await httpClient.GetAsync(swaggerJsonUrl);
        swaggerResponse.EnsureSuccessStatusCode();
        var swaggerJson = await swaggerResponse.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(swaggerJson);
        var tags = doc.RootElement.GetProperty("tags");

        var containsWalletTag = tags.EnumerateArray().Any(tag => tag.TryGetProperty("name", out var name) && name.GetString() == "Wallet");

        containsWalletTag.Should().BeTrue("Swagger JSON should contain a 'Wallet' tag.");
    }

    [Fact]
    public async Task SwaggerJson_WalletTagHasCorrectContent()
    {
        var httpClient = _serverFixture.CreateHttpClient();
        var swaggerJsonUrl = "swagger/v1/swagger.json";
        var swaggerResponse = await httpClient.GetAsync(swaggerJsonUrl);
        swaggerResponse.EnsureSuccessStatusCode();
        var swaggerJson = await swaggerResponse.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(swaggerJson);
        var tags = doc.RootElement.GetProperty("tags");

        var walletTag = tags.EnumerateArray()
            .FirstOrDefault(tag => tag.GetProperty("name").GetString() == "Wallet");

        var tagDetails = new
        {
            Name = walletTag.GetProperty("name").GetString(),
            Description = walletTag.GetProperty("description").GetString()
        };

        await Verifier.Verify(tagDetails)
            .UseMethodName("SwaggerJson_VerifyWalletTagContent");
    }

    [Fact]
    public async Task can_create_wallet_endpoint()
    {
        //Arrange
        var owner = _fixture.Create<string>();
        var someOwnerName = _fixture.Create<string>();
        var httpClient = CreateAuthenticatedHttpClient(owner, someOwnerName);

        var httpResponse = await httpClient.PostAsJsonAsync("v1/wallets", new { });
        var walletResponse = await httpResponse.Content.ReadFromJsonAsync<CreateWalletResponse>();

        //Act
        var res = await httpClient.PostAsJsonAsync($"v1/wallets/{walletResponse!.WalletId}/endpoints", new { });

        //Assert
        var content = await res.Content.ReadAsStringAsync();
        await Verifier.VerifyJson(content)
            .ScrubMember("publicKey");
    }
}
