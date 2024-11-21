using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectOrigin.PedersenCommitment;
using System;
using System.Linq;
using Xunit.Sdk;
using ProjectOrigin.Vault.Services.REST.v1;
using System.Net.Http.Json;

namespace ProjectOrigin.Vault.Tests;

public abstract class AbstractFlowTests
{
    protected readonly WalletSystemTestFixture WalletTestFixture;

    public AbstractFlowTests(WalletSystemTestFixture walletTestFixture)
    {
        WalletTestFixture = walletTestFixture;
    }

    protected async Task<FederatedStreamId> IssueCertificateToEndpoint(
        WalletEndpointReference endpoint,
        Electricity.V1.GranularCertificateType type,
        SecretCommitmentInfo issuedCommitment,
        int position,
        List<(string Key, string Value, byte[]? Salt)>? attributes = null)
    {
        var issuedEvent = await WalletTestFixture.StampAndRegistryFixture.IssueCertificate(
            type,
            issuedCommitment,
            endpoint.PublicKey.Derive(position).GetPublicKey(),
            attributes);

        var client = WalletTestFixture.ServerFixture.CreateHttpClient();

        var receiveRequest = new ReceiveRequest
        {
            CertificateId = new FederatedStreamId()
            {
                Registry = issuedEvent.CertificateId.Registry,
                StreamId = Guid.Parse(issuedEvent.CertificateId.StreamId.Value)
            },
            PublicKey = endpoint.PublicKey.Export().ToArray(),
            Position = (uint)position,
            Quantity = issuedCommitment.Message,
            RandomR = issuedCommitment.BlindingValue.ToArray(),
            HashedAttributes = attributes?.Where(attribute => attribute.Salt is not null).Select(attribute => new HashedAttribute
            {
                Key = attribute.Key,
                Value = attribute.Value,
                Salt = attribute.Salt!
            }) ?? []
        };

        var response = await client.PostAsJsonAsync("v1/slices", receiveRequest);
        response.EnsureSuccessStatusCode();

        return new FederatedStreamId
        {
            Registry = issuedEvent.CertificateId.Registry,
            StreamId = Guid.Parse(issuedEvent.CertificateId.StreamId.Value)
        };
    }

    protected static async Task<T> Timeout<T>(Func<Task<T>> func, TimeSpan timeout)
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
