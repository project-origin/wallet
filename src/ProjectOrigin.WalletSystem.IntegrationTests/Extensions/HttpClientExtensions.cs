using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.WalletSystem.Server.Serialization;
using ProjectOrigin.WalletSystem.Server.Services.REST.v1;
using Xunit.Sdk;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public static class HttpClientExtensions
{
    public static Task<CreateWalletResponse> CreateWallet(this HttpClient client) =>
        client.PostAsJsonAsync("v1/wallets", new { }).ParseJson<CreateWalletResponse>();

    public static Task<CreateWalletEndpointResponse> CreateWalletEndpoint(this HttpClient client, Guid walletId) =>
        client.PostAsJsonAsync($"v1/wallets/{walletId}/endpoints", new { }).ParseJson<CreateWalletEndpointResponse>();

    public static Task<CreateExternalEndpointResponse> CreateExternalEndpoint(this HttpClient client, CreateExternalEndpointRequest request)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new IHDPublicKeyConverter(new Secp256k1Algorithm()));
        return client.PostAsJsonAsync($"v1/external-endpoints", request, options).ParseJson<CreateExternalEndpointResponse>();
    }

    public static Task<ResultList<GranularCertificate, PageInfo>> GetCertificates(this HttpClient client) =>
        client.GetAsync($"v1/certificates").ParseJson<ResultList<GranularCertificate, PageInfo>>();

    public static Task<IEnumerable<GranularCertificate>> GetCertificatesWithTimeout(this HttpClient client, int count, TimeSpan timeout) =>
        Timeout(async () =>
        {
            var response = await client.GetCertificates();
            response.Result.Should().HaveCount(count);
            return response.Result;
        }, timeout);

    public static Task<ClaimResponse> CreateClaim(this HttpClient client, FederatedStreamId consumptionCertificateId, FederatedStreamId productionCertificateId, uint quantity) =>
        client.PostAsJsonAsync("v1/claims",
            new ClaimRequest
            {
                ConsumptionCertificateId = consumptionCertificateId,
                ProductionCertificateId = productionCertificateId,
                Quantity = quantity
            })
        .ParseJson<ClaimResponse>();

    public static Task<TransferResponse> CreateTransfer(this HttpClient client, TransferRequest request) =>
        client.PostAsJsonAsync("v1/transfers", request).ParseJson<TransferResponse>();

    private static async Task<T> Timeout<T>(Func<Task<T>> func, TimeSpan timeout)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                return await func();
            }
            catch (XunitException)
            {
                await Task.Delay(1000);
            }
        }
        throw new TimeoutException();
    }
}
