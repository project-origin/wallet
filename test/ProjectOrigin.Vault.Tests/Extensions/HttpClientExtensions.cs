using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.Vault.Serialization;
using ProjectOrigin.Vault.Services.REST.v1;
using Xunit.Sdk;

namespace ProjectOrigin.Vault.Tests;

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

    public static Task<CreateRecipientResponse> StampCreateRecipient(this HttpClient client, CreateRecipientRequest request) =>
        client.PostAsJsonAsync($"v1/recipients", request).ParseJson<CreateRecipientResponse>();

    public static Task<IssueCertificateResponse> StampIssueCertificate(this HttpClient client, CreateCertificateRequest request) =>
        client.PostAsJsonAsync($"v1/certificates", request).ParseJson<IssueCertificateResponse>();

    public static Task<WithdrawnCertificateDto> StampWithdrawCertificate(this HttpClient client, string registry, Guid certificateId) =>
            client.PostAsJsonAsync($"v1/certificates/{registry}/{certificateId}/withdraw", new { }).ParseJson<WithdrawnCertificateDto>();

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

#region stamp dtos

/// <summary>
/// Request to create a new recipient.
/// </summary>
public record CreateRecipientRequest
{
    /// <summary>
    /// The recipient wallet endpoint reference.
    /// </summary>
    public required StampWalletEndpointReferenceDto WalletEndpointReference { get; init; }
}

public record StampWalletEndpointReferenceDto
{
    /// <summary>
    /// The version of the ReceiveSlice API.
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    /// The url endpoint of where the wallet is hosted.
    /// </summary>
    public required Uri Endpoint { get; init; }

    /// <summary>
    /// The public key used to generate sub-public-keys for each slice.
    /// </summary>
    public required byte[] PublicKey { get; init; }
}

/// <summary>
/// Response to create a recipient.
/// </summary>
public record CreateRecipientResponse
{
    /// <summary>
    /// The ID of the created recipient.
    /// </summary>
    public required Guid Id { get; init; }
}

public record CreateCertificateRequest
{
    /// <summary>
    /// The recipient id of the certificate.
    /// </summary>
    public required Guid RecipientId { get; init; }

    /// <summary>
    /// The registry used to issues the certificate.
    /// </summary>
    public required string RegistryName { get; init; }

    /// <summary>
    /// The id of the metering point used to produce the certificate.
    /// </summary>
    public required string MeteringPointId { get; init; }

    /// <summary>
    /// The certificate to issue.
    /// </summary>
    public required StampCertificateDto Certificate { get; init; }
}

public record StampCertificateDto
{
    /// <summary>
    /// The id of the certificate.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The type of certificate (production or consumption).
    /// </summary>
    public required StampCertificateType Type { get; init; }

    /// <summary>
    /// The quantity available on the certificate.
    /// </summary>
    public required uint Quantity { get; init; }

    /// <summary>
    /// The start of the period for which the certificate is valid.
    /// </summary>
    public required long Start { get; init; }

    /// <summary>
    /// The end of the period for which the certificate is valid.
    /// </summary>
    public required long End { get; init; }

    /// <summary>
    /// The Grid Area of the certificate.
    /// </summary>
    public required string GridArea { get; init; }

    /// <summary>
    /// Attributes of the certificate that is not hashed.
    /// </summary>
    public required Dictionary<string, string> ClearTextAttributes { get; init; }

    /// <summary>
    /// List of hashed attributes, their values and salts so the receiver can access the data.
    /// </summary>
    public required IEnumerable<StampHashedAttribute> HashedAttributes { get; init; }
}

public enum StampCertificateType
{
    Consumption = 1,
    Production = 2
}

/// <summary>
/// Hashed attribute with salt.
/// </summary>
public record StampHashedAttribute()
{
    /// <summary>
    /// The key of the attribute.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The value of the attribute.
    /// </summary>
    public required string Value { get; init; }
}

/// <summary>
/// Response to issue certificate request.
/// </summary>
public record IssueCertificateResponse() { }

public record WithdrawnCertificateDto
{
    public required int Id { get; init; }
    public required string RegistryName { get; init; }
    public required Guid CertificateId { get; init; }
    public required DateTimeOffset WithdrawnDate { get; init; }
}

#endregion
