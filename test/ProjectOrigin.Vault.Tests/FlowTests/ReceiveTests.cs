using System.Threading.Tasks;
using Xunit;
using ProjectOrigin.PedersenCommitment;
using FluentAssertions;
using System;
using ProjectOrigin.Vault.Services.REST.v1;
using System.Net.Http.Headers;

namespace ProjectOrigin.Vault.Tests.FlowTests;

[Collection(WalletSystemTestCollection.CollectionName)]
public class ReceiveTests : AbstractFlowTests
{
    public ReceiveTests(WalletSystemTestFixture walletTestFixture) : base(walletTestFixture)
    {
    }

    [Fact]
    public async Task IssueCertWithAndWithoutAttributes_Query_Success()
    {
        var position = 1;

        var client = WalletTestFixture.ServerFixture.CreateHttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WalletTestFixture.JwtTokenIssuerFixture.GenerateRandomToken());

        var wallet = await client.CreateWallet();
        var endpoint = await client.CreateWalletEndpoint(wallet.WalletId);

        var prodCertId = await IssueCertificateToEndpoint(
            endpoint.WalletReference,
            Electricity.V1.GranularCertificateType.Production,
            new SecretCommitmentInfo(250),
            position++,
            [
                ("TechCode", "T010101", null),
                ("FuelCode", "F010101", null),
            ]);

        var conCertId = await IssueCertificateToEndpoint(
            endpoint.WalletReference,
            Electricity.V1.GranularCertificateType.Consumption,
            new SecretCommitmentInfo(150),
            position++);

        var certificates = await client.GetCertificatesWithTimeout(2, TimeSpan.FromMinutes(1));

        var gc1 = certificates.Should().Contain(x => x.FederatedStreamId.StreamId == prodCertId.StreamId).Which;
        gc1.CertificateType.Should().Be(CertificateType.Production);
        gc1.Quantity.Should().Be(250);
        gc1.Attributes.Should().HaveCount(2);

        var gc2 = certificates.Should().Contain(x => x.FederatedStreamId.StreamId == conCertId.StreamId).Which;
        gc2.CertificateType.Should().Be(CertificateType.Consumption);
        gc2.Quantity.Should().Be(150);
        gc2.Attributes.Should().HaveCount(0);
    }

    [Fact]
    public async Task IssueCertWithAttributes_Query_Success()
    {
        var position = 1;

        var client = WalletTestFixture.ServerFixture.CreateHttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WalletTestFixture.JwtTokenIssuerFixture.GenerateRandomToken());

        var wallet = await client.CreateWallet();
        var endpoint = await client.CreateWalletEndpoint(wallet.WalletId);

        var certificateId = await IssueCertificateToEndpoint(
            endpoint.WalletReference,
            Electricity.V1.GranularCertificateType.Production,
            new SecretCommitmentInfo(250),
            position++,
            new(){
                ("techCode", "T010101", null),
                ("fuelCode", "F010101", null),
                ("assetId", "1264541", new byte[] { 0x01, 0x02, 0x03, 0x04 }),
            });

        var certificates = await client.GetCertificatesWithTimeout(1, TimeSpan.FromMinutes(1));

        var foundCertificate = certificates.Should().Contain(x => x.FederatedStreamId.StreamId == certificateId.StreamId).Which;
        foundCertificate.CertificateType.Should().Be(CertificateType.Production);
        foundCertificate.Quantity.Should().Be(250);
        foundCertificate.Attributes.Should().HaveCount(3)
            .And.Contain(x => x.Key == "techCode" && x.Value == "T010101")
            .And.Contain(x => x.Key == "fuelCode" && x.Value == "F010101")
            .And.Contain(x => x.Key == "assetId" && x.Value == "1264541");
    }
}
