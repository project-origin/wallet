using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FluentAssertions;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.WalletSystem.Server.Serialization;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public static class HttpTaskExtensions
{
    private static Lazy<JsonSerializerOptions> JsonOptions = new Lazy<JsonSerializerOptions>(() =>
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new IHDPublicKeyConverter(new Secp256k1Algorithm()));
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    });

    public static async Task<T> ParseJson<T>(this Task<HttpResponseMessage> httpResponse)
    {
        var response = await httpResponse;
        response.IsSuccessStatusCode.Should().BeTrue();
        var responseString = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<T>(responseString, JsonOptions.Value)
            ?? throw new Exception("Failed to deserialize");
    }
}
