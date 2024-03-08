using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using ProjectOrigin.PedersenCommitment;
using System.Linq;
using FluentAssertions;
using AutoFixture;
using System.Net.Http.Headers;
using ProjectOrigin.WalletSystem.Server.Services.REST.v1;
using System.Net.Http;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class TransferTests : AbstractFlowTests
{
    public TransferTests(
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
    }

    [Fact]
    public async Task Transfer_SingleSlice_LocalWallet()
    {
        //Arrange
        var issuedAmount = 250u;
        var transferredAmount = 150u;

        // Create recipient wallet
        var (recipientEndpoint, recipientClient) = await CreateWalletEndpointAndHttpClient();

        // Create sender wallet
        var (senderEndpoint, senderClient) = await CreateWalletEndpointAndHttpClient();

        // Issue certificate to sender
        var certificateId = await IssueCertificateToEndpoint(senderEndpoint, Electricity.V1.GranularCertificateType.Production, new SecretCommitmentInfo(issuedAmount), 1);
        await senderClient.GetCertificatesWithTimeout(1, TimeSpan.FromMinutes(1));

        // Create external endpoint
        var externalEndpoint = await senderClient.CreateExternalEndpoint(new()
        {
            TextReference = _fixture.Create<string>(),
            WalletReference = recipientEndpoint
        });

        //Act
        await senderClient.CreateTransfer(new()
        {
            CertificateId = certificateId,
            Quantity = transferredAmount,
            ReceiverId = externalEndpoint.ReceiverId,
            HashedAttributes = []
        });

        //Assert
        var response = await recipientClient.GetCertificatesWithTimeout(1, TimeSpan.FromMinutes(1));
        response.Single().Quantity.Should().Be(transferredAmount);
    }

    [Fact]
    public async Task Transfer_MultipleSlice_LocalWallet()
    {
        //Arrange
        var issuedAmount = 500u;

        // Create sender wallet
        var (senderEndpoint, senderClient) = await CreateWalletEndpointAndHttpClient();
        var (intermidiateEndpoint, intermidiateClient) = await CreateWalletEndpointAndHttpClient();
        var (recipientEndpoint, recipientClient) = await CreateWalletEndpointAndHttpClient();

        // Issue certificate to sender
        var certificateId = await IssueCertificateToEndpoint(senderEndpoint, Electricity.V1.GranularCertificateType.Production, new SecretCommitmentInfo(issuedAmount), 1);
        await senderClient.GetCertificatesWithTimeout(1, TimeSpan.FromMinutes(1));

        // Send 250 to intermidiate wallet and wait for it to be received
        var intermidiateReference = await senderClient.CreateExternalEndpoint(new()
        {
            TextReference = _fixture.Create<string>(),
            WalletReference = intermidiateEndpoint
        });
        await senderClient.CreateTransfer(new()
        {
            CertificateId = certificateId,
            Quantity = 250u,
            ReceiverId = intermidiateReference.ReceiverId,
            HashedAttributes = []
        });
        await intermidiateClient.GetCertificatesWithTimeout(1, TimeSpan.FromMinutes(2));

        // Send 250 to intermidiate wallet and wait for it to be received
        var recipientReference = await intermidiateClient.CreateExternalEndpoint(new()
        {
            TextReference = _fixture.Create<string>(),
            WalletReference = recipientEndpoint
        });
        await intermidiateClient.CreateTransfer(new()
        {
            CertificateId = certificateId,
            Quantity = 150u,
            ReceiverId = recipientReference.ReceiverId,
            HashedAttributes = []
        });

        //Asset
        var recipientCertificates = await recipientClient.GetCertificatesWithTimeout(1, TimeSpan.FromMinutes(1));
        recipientCertificates.Single().Quantity.Should().Be(150u);

        var intermidiateCertificates = await intermidiateClient.GetCertificatesWithTimeout(1, TimeSpan.FromMinutes(1));
        intermidiateCertificates.Single().Quantity.Should().Be(100u);

        var senderCertificates = await senderClient.GetCertificatesWithTimeout(1, TimeSpan.FromMinutes(1));
        senderCertificates.Single().Quantity.Should().Be(250u);
    }

    [Theory]
    [InlineData(250, 150)] // transfer part of slice
    [InlineData(250, 250)] // transfer whole slice
    public async Task Transfer_WithHashedAttributes(uint issuedAmount, uint transferredAmount)
    {
        //Arrange
        var (senderEndpoint, senderClient) = await CreateWalletEndpointAndHttpClient();
        var (recipientEndpoint, recipientClient) = await CreateWalletEndpointAndHttpClient();

        // Issue certificate to sender
        var certificateId = await IssueCertificateToEndpoint(
            senderEndpoint,
            Electricity.V1.GranularCertificateType.Production,
            new SecretCommitmentInfo(issuedAmount),
            1,
            [
                ("TechCode", "T010101", null),
                ("FuelCode", "F010101", null),
                ("AssetId", "1264541", new byte[] { 0x01, 0x02, 0x03, 0x04 }),
            ]);
        await senderClient.GetCertificatesWithTimeout(1, TimeSpan.FromMinutes(1));

        // Create external endpoint
        var externalEndpoint = await senderClient.CreateExternalEndpoint(new()
        {
            TextReference = _fixture.Create<string>(),
            WalletReference = recipientEndpoint
        });

        //Act
        await senderClient.CreateTransfer(new()
        {
            CertificateId = certificateId,
            Quantity = transferredAmount,
            ReceiverId = externalEndpoint.ReceiverId,
            HashedAttributes = [
                "TechCode",
                "FuelCode",
                "AssetId",
            ]
        });

        //Assert
        var response = await recipientClient.GetCertificatesWithTimeout(1, TimeSpan.FromMinutes(1));
        response.Single().Attributes.Should().HaveCount(3);
        response.Single().Attributes.Should().Contain(x => x.Key == "AssetId" && x.Value == "1264541");
    }

    private async Task<(WalletEndpointReference, HttpClient)> CreateWalletEndpointAndHttpClient()
    {
        var client = _serverFixture.CreateHttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtTokenIssuer.GenerateRandomToken());
        var wallet = await client.CreateWallet();
        var endpoint = await client.CreateWalletEndpoint(wallet.WalletId);

        return (endpoint.WalletReference, client);
    }
}
