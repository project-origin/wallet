using AutoFixture;
using FluentAssertions;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.Vault.Services.REST.v1;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.Vault.Tests.FlowTests;

[Collection(WalletSystemTestCollection.CollectionName)]
public class TransferTests : AbstractFlowTests
{
    private readonly Fixture _fixture;

    public TransferTests(WalletSystemTestFixture walletTestFixture) : base(walletTestFixture)
    {
        _fixture = new Fixture();
    }

    [Fact]
    public async Task Transfer_UnknownCertificate_BadRequest()
    {
        //Arrange
        var transferredAmount = 150u;

        // Create recipient wallet
        var (recipientEndpoint, recipientClient) = await CreateWalletEndpointAndHttpClient();

        // Create sender wallet
        var (senderEndpoint, senderClient) = await CreateWalletEndpointAndHttpClient();

        // Create external endpoint
        var externalEndpoint = await senderClient.CreateExternalEndpoint(new()
        {
            TextReference = _fixture.Create<string>(),
            WalletReference = recipientEndpoint
        });

        //Act
        var request = new TransferRequest()
        {
            CertificateId = new FederatedStreamId()
            {
                Registry = "SomeRegistry",
                StreamId = Guid.NewGuid()
            },
            Quantity = transferredAmount,
            ReceiverId = externalEndpoint.ReceiverId,
            HashedAttributes = []
        };
        var response = await senderClient.PostAsync("v1/transfers", JsonContent.Create(request));

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_MoreThanAvailableOnTheCertificate_BadRequest()
    {
        //Arrange
        var issuedAmount = 250u;
        var transferredAmount = 300u;

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
        var request = new TransferRequest()
        {
            CertificateId = certificateId,
            Quantity = transferredAmount,
            ReceiverId = externalEndpoint.ReceiverId,
            HashedAttributes = []
        };
        var response = await senderClient.PostAsync("v1/transfers", JsonContent.Create(request));

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_UnknownReceiver_BadRequest()
    {
        //Arrange
        var issuedAmount = 250u;
        var transferredAmount = 150u;

        // Create sender wallet
        var (senderEndpoint, senderClient) = await CreateWalletEndpointAndHttpClient();

        // Issue certificate to sender
        var certificateId = await IssueCertificateToEndpoint(senderEndpoint, Electricity.V1.GranularCertificateType.Production, new SecretCommitmentInfo(issuedAmount), 1);
        await senderClient.GetCertificatesWithTimeout(1, TimeSpan.FromMinutes(1));

        //Act
        var request = new TransferRequest()
        {
            CertificateId = certificateId,
            Quantity = transferredAmount,
            ReceiverId = Guid.NewGuid(),
            HashedAttributes = []
        };
        var response = await senderClient.PostAsync("v1/transfers", JsonContent.Create(request));

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
            attributes: [
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
                "AssetId"
            ]
        });

        //Assert
        var response = await recipientClient.GetCertificatesWithTimeout(1, TimeSpan.FromMinutes(1));
        response.Single().Attributes.Should().HaveCount(3);
        response.Single().Attributes.Should().Contain(x => x.Key == "assetId" && x.Value == "1264541");
    }

    private async Task<(WalletEndpointReference, HttpClient)> CreateWalletEndpointAndHttpClient()
    {
        var client = WalletTestFixture.ServerFixture.CreateHttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WalletTestFixture.JwtTokenIssuerFixture.GenerateRandomToken());
        var wallet = await client.CreateWallet();
        var endpoint = await client.CreateWalletEndpoint(wallet.WalletId);

        return (endpoint.WalletReference, client);
    }
}
