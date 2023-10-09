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
using ProjectOrigin.PedersenCommitment;
using System.Linq;
using FluentAssertions;
using ProjectOrigin.WalletSystem.IntegrationTests.TestExtensions;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class TransferCertificateTests : WalletSystemTestsBase, IClassFixture<RegistryFixture>, IClassFixture<InMemoryFixture>
{
    private readonly RegistryFixture _registryFixture;

    public TransferCertificateTests(
            GrpcTestFixture<Startup> grpcFixture,
            PostgresDatabaseFixture dbFixture,
            InMemoryFixture inMemoryFixture,
            RegistryFixture registryFixture,
            ITestOutputHelper outputHelper)
            : base(
                  grpcFixture,
                  dbFixture,
                  inMemoryFixture,
                  outputHelper,
                  registryFixture)
    {
        _registryFixture = registryFixture;
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
        var senderEndpoint = await _dbFixture.CreateWalletEndpoint(sender);
        var position = 1;
        var issuedEvent = await _registryFixture.IssueCertificate(Electricity.V1.GranularCertificateType.Production, commitment, senderEndpoint.PublicKey.Derive(position).GetPublicKey());
        var certId = Guid.Parse(issuedEvent.CertificateId.StreamId.Value);
        await _dbFixture.InsertSlice(senderEndpoint, position, issuedEvent, commitment);

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
        await WaitForCertCount(certId, 3);
    }

    [Fact]
    public async Task Transfer_MultipleSlice_LocalWallet()
    {
        //Arrange
        var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);

        // Create sender wallet
        var (sender, senderHeader) = GenerateUserHeader();
        var endpoint = await _dbFixture.CreateWalletEndpoint(sender);
        var position = await _dbFixture.GetNextNumberForId(endpoint.Id);

        // Create remainder endpoint and increment position to force test to fail if positions are calculated incorrectly
        var remainderEndpoint = await _dbFixture.GetWalletRemainderEndpoint(endpoint.WalletId);
        await _dbFixture.GetNextNumberForId(remainderEndpoint.Id);
        await _dbFixture.GetNextNumberForId(remainderEndpoint.Id);

        // Issue certificate to sender
        var issuedAmount = 500u;
        var commitment = new SecretCommitmentInfo(issuedAmount);
        var issuedEvent = await _registryFixture.IssueCertificate(Electricity.V1.GranularCertificateType.Production, commitment, endpoint.PublicKey.Derive(position).GetPublicKey());
        await _dbFixture.InsertSlice(endpoint, position, issuedEvent, commitment);
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
        await WaitForCertCount(certId, 3);

        await client.TransferCertificateAsync(new TransferRequest()
        {
            CertificateId = issuedEvent.CertificateId,
            Quantity = 100u,
            ReceiverId = senderItermidiateCER.ReceiverId
        }, senderHeader);

        //Assert
        await WaitForCertCount(certId, 5);

        // Create recipient wallet
        var (recipient, recipientHeader) = GenerateUserHeader();
        var recipientCER = await client.CreateWalletDepositEndpointAsync(new CreateWalletDepositEndpointRequest(), recipientHeader);
        var intermidiateRecipientCER = await client.CreateReceiverDepositEndpointAsync(new CreateReceiverDepositEndpointRequest
        {
            WalletDepositEndpoint = recipientCER.WalletDepositEndpoint
        }, intermidiateHeader);

        //Act
        await client.TransferCertificateAsync(new TransferRequest()
        {
            CertificateId = issuedEvent.CertificateId,
            Quantity = 200u, // transfer more than a single slice but less that both slices
            ReceiverId = intermidiateRecipientCER.ReceiverId
        }, intermidiateHeader);

        await WaitForCertCount(certId, 8);
    }

    [Fact(Skip = "Not implemented")]
    public Task Transfer_SingleSlice_ExternalWallet()
    {
        // Requires an seperate walletSystem instance to send the ReceiveSlice request to.
        throw new NotImplementedException();
    }

    private async Task WaitForCertCount(Guid certId, int number)
    {
        await using var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        var startedAt = DateTime.UtcNow;
        var slicesFound = 0;
        while (DateTime.UtcNow - startedAt < TimeSpan.FromMinutes(1))
        {
            // Verify slice created in database
            var slices = await connection.QueryAsync<WalletSlice>("SELECT * FROM wallet_slices s WHERE certificate_id = @certificateId", new { certificateId = certId });
            slicesFound = slices.Count();
            if (slicesFound >= number)
                break;
            await Task.Delay(1000);
        }
        slicesFound.Should().Be(number, "correct number of slices should be found");
    }
}
