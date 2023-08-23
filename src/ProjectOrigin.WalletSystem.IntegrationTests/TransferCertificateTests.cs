using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System;
using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.V1;
using Xunit;
using Npgsql;
using Dapper;
using ProjectOrigin.WalletSystem.Server.Models;
using Xunit.Abstractions;
using AutoFixture;
using ProjectOrigin.PedersenCommitment;
using Grpc.Core;
using System.Linq;
using FluentAssertions;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class TransferCertificateTests : WalletSystemTestsBase, IClassFixture<RegistryFixture>
{
    private RegistryFixture _registryFixture;
    private Fixture _fixture;

    public TransferCertificateTests(GrpcTestFixture<Startup> grpcFixture, RegistryFixture registryFixture, PostgresDatabaseFixture dbFixture, ITestOutputHelper outputHelper)
        : base(grpcFixture, dbFixture, outputHelper, registryFixture)
    {
        _registryFixture = registryFixture;
        _fixture = new Fixture();
    }

    [Fact]
    public async Task Transfer_SingleSlice_LocalWallet()
    {
        //Arrange
        var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);
        var issuedAmount = 250u;
        var transferredAmount = 150u;

        var (sender, senderHeader) = GenerateUserHeader();
        var commitment = new SecretCommitmentInfo(issuedAmount);
        var senderDepositEndpoint = await CreateWalletDepositEndpoint(sender);
        var position = 1;
        var issuedEvent = await _registryFixture.IssueCertificate(Electricity.V1.GranularCertificateType.Production, commitment, senderDepositEndpoint.PublicKey.Derive(position).GetPublicKey());
        var certId = Guid.Parse(issuedEvent.CertificateId.StreamId.Value);
        await InsertSlice(senderDepositEndpoint, position, issuedEvent, commitment);

        var (recipient, recipientHeader) = GenerateUserHeader();
        var createEndpointResponse = await client.CreateWalletDepositEndpointAsync(new CreateWalletDepositEndpointRequest(), recipientHeader);
        var receiverEndpointResponse = await client.CreateReceiverDepositEndpointAsync(new CreateReceiverDepositEndpointRequest
        {
            WalletDepositEndpoint = createEndpointResponse.WalletDepositEndpoint
        }, senderHeader);

        //Act
        var request = new TransferRequest()
        {
            CertificateId = issuedEvent.CertificateId,
            Quantity = transferredAmount,
            ReceiverId = receiverEndpointResponse.ReceiverId
        };
        await client.TransferCertificateAsync(request, senderHeader);

        //Assert
        await WaitForCertCount(certId, 4);
    }

    [Fact]
    public async Task Transfer_EntireCertificate_LocalWallet()
    {
        //Arrange
        var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);
        var issuedAmount = 250u;
        var transferredAmount = issuedAmount;

        var (sender, senderHeader) = GenerateUserHeader();
        var commitment = new SecretCommitmentInfo(issuedAmount);
        var senderDepositEndpoint = await CreateWalletDepositEndpoint(sender);
        var position = 1;
        var issuedEvent = await _registryFixture.IssueCertificate(Electricity.V1.GranularCertificateType.Production, commitment, senderDepositEndpoint.PublicKey.Derive(position).GetPublicKey());
        var certId = Guid.Parse(issuedEvent.CertificateId.StreamId.Value);
        await InsertSlice(senderDepositEndpoint, position, issuedEvent, commitment);

        var (_, recipientHeader) = GenerateUserHeader();
        var createEndpointResponse = await client.CreateWalletDepositEndpointAsync(new CreateWalletDepositEndpointRequest(), recipientHeader);
        var receiverEndpointResponse = await client.CreateReceiverDepositEndpointAsync(new CreateReceiverDepositEndpointRequest
        {
            WalletDepositEndpoint = createEndpointResponse.WalletDepositEndpoint
        }, senderHeader);

        //Act
        var request = new TransferRequest()
        {
            CertificateId = issuedEvent.CertificateId,
            Quantity = transferredAmount,
            ReceiverId = receiverEndpointResponse.ReceiverId
        };
        await client.TransferCertificateAsync(request, senderHeader);

        //Assert
        await WaitForCertCount(client, recipientHeader, 1);

        var senderCertsAfter = await client.QueryGranularCertificatesAsync(new QueryRequest(), senderHeader);
        senderCertsAfter.GranularCertificates.Should().HaveCount(0);

        var receiverCertsAfter = await client.QueryGranularCertificatesAsync(new QueryRequest(), recipientHeader);
        receiverCertsAfter.GranularCertificates.Should().HaveCount(1);
        receiverCertsAfter.GranularCertificates.Single().FederatedId.StreamId.Value.Should().Be(certId.ToString());
    }

    [Fact]
    public async Task Transfer_MultipleSlice_LocalWallet()
    {
        //Arrange
        var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);

        // Create sender wallet
        var (sender, senderHeader) = GenerateUserHeader();
        var depositEndpoint = await CreateWalletDepositEndpoint(sender);

        // Issue certificate to sender
        var position = 1;
        var issuedAmount = 500u;
        var commitment = new SecretCommitmentInfo(issuedAmount);
        var issuedEvent = await _registryFixture.IssueCertificate(Electricity.V1.GranularCertificateType.Production, commitment, depositEndpoint.PublicKey.Derive(position).GetPublicKey());
        await InsertSlice(depositEndpoint, position, issuedEvent, commitment);
        var certId = Guid.Parse(issuedEvent.CertificateId.StreamId.Value);

        // Create intermidiate wallet
        var (intermidiate, intermidiateHeader) = GenerateUserHeader();
        var intermidiateCER = await client.CreateWalletDepositEndpointAsync(new CreateWalletDepositEndpointRequest(), intermidiateHeader);
        var senderItermidiateCER = await client.CreateReceiverDepositEndpointAsync(new CreateReceiverDepositEndpointRequest
        {
            WalletDepositEndpoint = intermidiateCER.WalletDepositEndpoint
        }, senderHeader);

        // Transfer 2 slices to intermidiate wallet
        await client.TransferCertificateAsync(new TransferRequest()
        {
            CertificateId = issuedEvent.CertificateId,
            Quantity = 150u,
            ReceiverId = senderItermidiateCER.ReceiverId
        }, senderHeader);

        //Assert
        await WaitForCertCount(certId, 4);

        await client.TransferCertificateAsync(new TransferRequest()
        {
            CertificateId = issuedEvent.CertificateId,
            Quantity = 100u,
            ReceiverId = senderItermidiateCER.ReceiverId
        }, senderHeader);

        //Assert
        await WaitForCertCount(certId, 7);

        // Create recipient wallet
        var (recipient, recipientHeader) = GenerateUserHeader();
        var recipientCER = await client.CreateWalletDepositEndpointAsync(new CreateWalletDepositEndpointRequest(), recipientHeader);
        var intermidiateRecipientCER = await client.CreateReceiverDepositEndpointAsync(new CreateReceiverDepositEndpointRequest
        {
            WalletDepositEndpoint = recipientCER.WalletDepositEndpoint
        }, intermidiateHeader);

        //Act
        var request = new TransferRequest()
        {
            CertificateId = issuedEvent.CertificateId,
            Quantity = 200u, // transfer more than a single slice but less that both slices
            ReceiverId = senderItermidiateCER.ReceiverId
        };
        await client.TransferCertificateAsync(request, intermidiateHeader);

        await WaitForCertCount(certId, 12);
    }

    [Fact(Skip = "Not implemented")]
    public Task Transfer_SingleSlice_ExternalWallet()
    {
        // Requires an seperate walletSystem instance to send the ReceiveSlice request to.
        throw new NotImplementedException();
    }

    private static async Task WaitForCertCount(WalletService.WalletServiceClient client, Metadata header, int number)
    {
        var startedAt = DateTime.UtcNow;
        var certsFound = 0;
        while (DateTime.UtcNow - startedAt < TimeSpan.FromMinutes(1))
        {
            var certificates = await client.QueryGranularCertificatesAsync(new QueryRequest(), header);
            certsFound = certificates.GranularCertificates.Count;
            if (certsFound >= number)
                break;
            await Task.Delay(1000);
        }
        certsFound.Should().Be(number, "correct number of certificates should be returned");
    }

    private async Task WaitForCertCount(Guid certId, int number)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var startedAt = DateTime.UtcNow;
            var slicesFound = 0;
            while (DateTime.UtcNow - startedAt < TimeSpan.FromMinutes(1))
            {
                // Verify slice created in database
                var slices = await connection.QueryAsync<Slice>("SELECT * FROM Slices WHERE CertificateId = @certificateId", new { certificateId = certId });
                slicesFound = slices.Count();
                if (slicesFound >= number)
                    break;
                await Task.Delay(1000);
            }
            slicesFound.Should().Be(number, "correct number of slices should be found");
        }
    }

    private (string, Metadata) GenerateUserHeader()
    {
        var subject = _fixture.Create<string>();
        var name = _fixture.Create<string>();

        var token = _tokenGenerator.GenerateToken(subject, name);

        var headers = new Metadata
        {
            { "Authorization", $"Bearer {token}" }
        };

        return (subject, headers);
    }
}
