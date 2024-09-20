using ProjectOrigin.Vault.Tests.TestClassFixtures;
using System.Collections.Generic;
using Xunit.Abstractions;
using System.Threading.Tasks;
using ProjectOrigin.PedersenCommitment;
using Xunit;
using System;
using System.Linq;
using Xunit.Sdk;
using ProjectOrigin.Vault.Services.REST.v1;
using System.Net.Http.Json;

namespace ProjectOrigin.Vault.Tests;

public abstract class AbstractFlowTests : WalletSystemTestsBase, IClassFixture<RegistryFixture>, IClassFixture<InMemoryFixture>, IClassFixture<JwtTokenIssuerFixture>
{
    private readonly RegistryFixture _registryFixture;
    protected readonly JwtTokenIssuerFixture _jwtTokenIssuer;

    public AbstractFlowTests(
        TestServerFixture<Startup> serverFixture,
        PostgresDatabaseFixture dbFixture,
        IMessageBrokerFixture messageBrokerFixture,
        JwtTokenIssuerFixture jwtTokenIssuerFixture,
        ITestOutputHelper outputHelper,
        RegistryFixture registryFixture) : base(serverFixture, dbFixture, messageBrokerFixture, jwtTokenIssuerFixture, outputHelper, registryFixture)
    {
        _registryFixture = registryFixture;
        _jwtTokenIssuer = jwtTokenIssuerFixture;
    }

    protected async Task<FederatedStreamId> IssueCertificateToEndpoint(
        WalletEndpointReference endpoint,
        Electricity.V1.GranularCertificateType type,
        SecretCommitmentInfo issuedCommitment,
        int position,
        List<(string Key, string Value, byte[]? Salt)>? attributes = null)
    {
        var issuedEvent = await _registryFixture.IssueCertificate(
            type,
            issuedCommitment,
            endpoint.PublicKey.Derive(position).GetPublicKey(),
            attributes);

        var client = _serverFixture.CreateHttpClient();

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
