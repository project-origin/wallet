using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
        var owner = _fixture.Create<string>();
        var someOwnerName = _fixture.Create<string>();
        var httpClient = _dockerTestFixture.CreateAuthenticatedHttpClient(owner, someOwnerName);
        var walletEndpoint = await CreateWalletAndEndpoint(httpClient);

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

        var certToExpireId = await IssueCertificate(stampClient,
            rResponse.Id,
            type,
            startDate: DateTimeOffset.UtcNow.AddDays(-_dockerTestFixture.DaysBeforeCertificatesExpire).AddHours(-1),
            endDate: DateTimeOffset.UtcNow.AddDays(-_dockerTestFixture.DaysBeforeCertificatesExpire));
        var certId = await IssueCertificate(stampClient,
            rResponse.Id,
            type,
            startDate: DateTimeOffset.UtcNow.AddHours(-1),
            endDate: DateTimeOffset.UtcNow);

        await Task.Delay(TimeSpan.FromSeconds(30));

        var res = await httpClient.GetAsync($"v1/certificates");
        var content = JsonConvert.DeserializeObject<ResultList<GranularCertificate, PageInfo>>(await res.Content.ReadAsStringAsync());

        content.Should().NotBeNull();
        content.Result.Count().Should().Be(1);
        content.Result.First().FederatedStreamId.StreamId.Should().Be(certId);
    }

    private async Task<WalletEndpointReference> CreateWalletAndEndpoint(HttpClient client)
    {
        var wallet = await client.CreateWallet();
        var walletEndpoint = await client.CreateWalletEndpoint(wallet.WalletId);
        return walletEndpoint.WalletReference;
    }

    private async Task<Guid> IssueCertificate(HttpClient stampClient, Guid recipientId, StampCertificateType type,
        DateTimeOffset startDate, DateTimeOffset endDate)
    {
        var gsrn = Some.Gsrn();
        var certificateId = Guid.NewGuid();
        var registryName = _dockerTestFixture.StampAndRegistryFixture.RegistryName;
        var issuerArea = _dockerTestFixture.StampAndRegistryFixture.IssuerArea;
        var icResponse = await stampClient.StampIssueCertificate(new CreateCertificateRequest
        {
            RecipientId = recipientId,
            RegistryName = registryName,
            MeteringPointId = gsrn,
            Certificate = new StampCertificateDto
            {
                Id = certificateId,
                Start = startDate.ToUnixTimeSeconds(),
                End = endDate.ToUnixTimeSeconds(),
                GridArea = issuerArea,
                Quantity = 123,
                Type = type,
                ClearTextAttributes = new Dictionary<string, string>
                {
                    { "fuelCode", Some.FuelCode },
                    { "techCode", Some.TechCode }
                },
                HashedAttributes = new List<StampHashedAttribute>
                {
                    new() { Key = "assetId", Value = gsrn },
                    new() { Key = "address", Value = "Some road 1234" }
                }
            }
        });
        return certificateId;
    }
}
