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

        var (owner, ownerHeader) = GenerateUserHeader();
        var commitment = new SecretCommitmentInfo(issuedAmount);
        var depositEndpoint = await CreateWalletDepositEndpoint(owner);
        var position = 1;
        var issuedEvent = await _registryFixture.IssueCertificate(Electricity.V1.GranularCertificateType.Production, commitment, depositEndpoint.PublicKey.Derive(position).GetPublicKey());
        var certId = Guid.Parse(issuedEvent.CertificateId.StreamId.Value);
        await InsertSlice(depositEndpoint, position, issuedEvent, commitment);

        var (recipient, recipientHeader) = GenerateUserHeader();
        var createEndpointResponse = await client.CreateWalletDepositEndpointAsync(new CreateWalletDepositEndpointRequest(), recipientHeader);
        var receiverEndpointResponse = await client.CreateReceiverDepositEndpointAsync(new CreateReceiverDepositEndpointRequest
        {
            WalletDepositEndpoint = createEndpointResponse.WalletDepositEndpoint
        }, recipientHeader);

        //Act
        var request = new TransferRequest()
        {
            CertificateId = issuedEvent.CertificateId,
            Quantity = transferredAmount,
            ReceiverId = receiverEndpointResponse.ReceiverId
        };
        await client.TransferCertificateAsync(request, ownerHeader);

        //Assert
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var startedAt = DateTime.UtcNow;
            var AllSlicesFound = false;
            while (DateTime.UtcNow - startedAt < TimeSpan.FromMinutes(1))
            {
                // Verify slice created in database
                var slices = await connection.QueryAsync<Slice>("SELECT * FROM Slices WHERE CertificateId = @certificateId", new { certificateId = certId });
                Console.WriteLine(slices.Count());
                if (slices.Count() == 4)
                {
                    AllSlicesFound = true;
                    break;
                }
                await Task.Delay(2500);
            }
            Assert.True(AllSlicesFound);
        }

        Assert.True(false);
    }

    [Fact(Skip = "Not implemented")]
    public Task Transfer_SingleSlice_ExternalWallet()
    {
        throw new NotImplementedException();
    }

    [Fact(Skip = "Not implemented")]
    public Task Transfer_MultipleSlice_LocalWallet()
    {
        throw new NotImplementedException();
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
