using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using AutoFixture;
using FluentAssertions;
using Newtonsoft.Json;
using ProjectOrigin.Vault.Services.REST.v1;

namespace ProjectOrigin.Vault.Tests.JobTests;

[Collection(DockerTestCollectionWithExpireTurnedOff.CollectionName)]
public class ExpireCertificatesJobTests 
{
    private readonly DockerTestFixtureWithExpireTurnedOff _dockerFixture;
    private readonly Fixture _fixture;

    public ExpireCertificatesJobTests(DockerTestFixtureWithExpireTurnedOff dockerFixture)
    {
        _dockerFixture = dockerFixture;
        _fixture = new Fixture();
    }

    [Fact]
    public async Task DoesNotRunWhenExpireDaysParamIsNull()
    {
        var registryName = _dockerFixture.StampAndRegistryFixture.RegistryName;
        var issuerArea = _dockerFixture.StampAndRegistryFixture.IssuerArea;
        var owner = _fixture.Create<string>();
        var someOwnerName = _fixture.Create<string>();
        var httpClient = _dockerFixture.CreateAuthenticatedHttpClient(owner, someOwnerName);
        var walletEndpoint = await httpClient.CreateWalletAndEndpoint();

        var stampClient = _dockerFixture.CreateStampClient();
        var rResponse = await stampClient.StampCreateRecipient(new CreateRecipientRequest
        {
            WalletEndpointReference = new StampWalletEndpointReferenceDto
            {
                Version = walletEndpoint.Version,
                Endpoint = walletEndpoint.Endpoint,
                PublicKey = walletEndpoint.PublicKey.Export().ToArray()
            }
        });

        var certToExpireId = await stampClient.IssueCertificate(registryName,
            issuerArea,
            rResponse.Id,
            StampCertificateType.Production,
            startDate: DateTimeOffset.UtcNow.AddDays(-61).AddHours(-1),
            endDate: DateTimeOffset.UtcNow.AddDays(-61));

        await Task.Delay(TimeSpan.FromSeconds(30));

        var res = await httpClient.GetAsync($"v1/certificates");
        var content = JsonConvert.DeserializeObject<ResultList<GranularCertificate, PageInfo>>(await res.Content.ReadAsStringAsync());

        content.Should().NotBeNull();
        content.Result.Count().Should().Be(1);
        content.Result.First().FederatedStreamId.StreamId.Should().Be(certToExpireId);
    }
}
