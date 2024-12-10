using System;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Newtonsoft.Json;
using ProjectOrigin.Vault.Services.REST.v1;
using Xunit;
using PageInfo = ProjectOrigin.Vault.Services.REST.v1.PageInfo;

namespace ProjectOrigin.Vault.Tests;

[Collection(DockerTestCollection.CollectionName)]
public class ExpireTests
{
    private readonly DockerTestFixture _dockerTestFixture;
    private readonly Fixture _fixture;

    public ExpireTests(DockerTestFixture dockerTestFixture)
    {
        _dockerTestFixture = dockerTestFixture;
        _fixture = new Fixture();
    }

    [Theory]
    [InlineData(StampCertificateType.Production)]
    [InlineData(StampCertificateType.Consumption)]
    public async Task ExpireCertificates(StampCertificateType type)
    {
        var registryName = _dockerTestFixture.StampAndRegistryFixture.RegistryName;
        var issuerArea = _dockerTestFixture.StampAndRegistryFixture.IssuerArea;
        var owner = _fixture.Create<string>();
        var someOwnerName = _fixture.Create<string>();
        var httpClient = _dockerTestFixture.CreateAuthenticatedHttpClient(owner, someOwnerName);
        var walletEndpoint = await httpClient.CreateWalletAndEndpoint();

        var stampClient = _dockerTestFixture.CreateStampClient();
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
            type,
            startDate: DateTimeOffset.UtcNow.AddDays(-_dockerTestFixture.DaysBeforeCertificatesExpire!.Value).AddHours(-1),
            endDate: DateTimeOffset.UtcNow.AddDays(-_dockerTestFixture.DaysBeforeCertificatesExpire!.Value));

        var certId = await stampClient.IssueCertificate(registryName,
            issuerArea,
            rResponse.Id,
            type,
            startDate: DateTimeOffset.UtcNow.AddHours(-1),
            endDate: DateTimeOffset.UtcNow);

        await Task.Delay(TimeSpan.FromSeconds(30));

        var res = await httpClient.GetAsync($"v1/certificates");
        var content = JsonConvert.DeserializeObject<ResultList<GranularCertificate, PageInfo>>(await res.Content.ReadAsStringAsync());

        content.Should().NotBeNull();
        content!.Result.Count().Should().Be(1);
        content.Result.First().FederatedStreamId.StreamId.Should().Be(certId);
    }
}
