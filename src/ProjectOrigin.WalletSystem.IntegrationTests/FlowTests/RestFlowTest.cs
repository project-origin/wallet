using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using System.Net.Http;
using System.Text;
using System.Net.Http.Headers;
using System;
using AutoFixture;
using System.Collections.Generic;
using ProjectOrigin.WalletSystem.Server.Services.REST.v1;
using ProjectOrigin.WalletSystem.Server.Serialization;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class RestFlowTest : AbstractFlowTests
{
    private readonly RegistryFixture _registryFixture;
    private readonly JwtTokenIssuerFixture _jwtTokenIssuer;

    public RestFlowTest(
            TestServerFixture<Startup> serverFixture,
            PostgresDatabaseFixture dbFixture,
            InMemoryFixture inMemoryFixture,
            JwtTokenIssuerFixture jwtTokenIssuerFixture,
            RegistryFixture registryFixture,
            ITestOutputHelper outputHelper)
            : base(
                  serverFixture,
                  dbFixture,
                  inMemoryFixture,
                  jwtTokenIssuerFixture,
                  outputHelper,
                  registryFixture)
    {
        _registryFixture = registryFixture;
        _jwtTokenIssuer = jwtTokenIssuerFixture;
    }

    [Fact]
    public async Task Transfer_SingleSlice_LocalWallet()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();

        var client = _serverFixture.CreateHttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtTokenIssuer.GenerateToken(subject, _fixture.Create<string>()));

        // create wallet
        var walletResponse = await client.PostAsync("v1/wallets", ToJsonContent(new { })).ParseJson<CreateWalletResponse>();

        // create wallet endpoint
        await client.PostAsync($"v1/wallets/{walletResponse.WalletId}/endpoints", ToJsonContent(new { })).ParseJson<CreateWalletEndpointResponse>();
    }

    private static HttpContent ToJsonContent(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}


public static class HttpTaskExtensions
{
    public static async Task<T> ParseJson<T>(this Task<HttpResponseMessage> httpResponse)
    {
        var response = await httpResponse;
        response.IsSuccessStatusCode.Should().BeTrue();
        var responseString = await response.Content.ReadAsStringAsync();

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new IHDPublicKeyConverter(new Secp256k1Algorithm()));
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        return JsonSerializer.Deserialize<T>(responseString, options) ?? throw new Exception("Failed to deserialize");
    }
}
