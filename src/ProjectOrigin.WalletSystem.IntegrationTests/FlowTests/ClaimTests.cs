using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System;
using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.V1;
using Xunit;
using ProjectOrigin.WalletSystem.Server.Models;
using Xunit.Abstractions;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.WalletSystem.IntegrationTests.TestExtensions;
using ProjectOrigin.Common.V1;
using Npgsql;
using Dapper;
using FluentAssertions;

namespace ProjectOrigin.WalletSystem.IntegrationTests.FlowTests;

public class ClaimTests : WalletSystemTestsBase, IClassFixture<RegistryFixture>, IClassFixture<InMemoryFixture>
{
    private readonly RegistryFixture _registryFixture;

    public ClaimTests(
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

    private async Task<FederatedStreamId> IssueCertToDepositEndpoint(DepositEndpoint senderDepositEndpoint, uint issuedAmount, Electricity.V1.GranularCertificateType type)
    {
        var prodCommitment = new SecretCommitmentInfo(issuedAmount);
        var position = 1;
        var issuedEvent = await _registryFixture.IssueCertificate(type, prodCommitment, senderDepositEndpoint.PublicKey.Derive(position).GetPublicKey());
        await _dbFixture.InsertSlice(senderDepositEndpoint, position, issuedEvent, prodCommitment);
        return issuedEvent.CertificateId;
    }

    [Fact]
    public async Task Transfer_SingleSlice_LocalWallet()
    {
        //Arrange
        var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);

        var (owner, header) = GenerateUserHeader();
        var senderDepositEndpoint = await _dbFixture.CreateWalletDepositEndpoint(owner);

        var consumptionId = await IssueCertToDepositEndpoint(senderDepositEndpoint, 150, Electricity.V1.GranularCertificateType.Consumption);
        var productionId = await IssueCertToDepositEndpoint(senderDepositEndpoint, 200, Electricity.V1.GranularCertificateType.Production);

        //Act
        var response = await client.ClaimCertificatesAsync(new ClaimRequest()
        {
            ConsumptionCertificateId = consumptionId,
            ProductionCertificateId = productionId,
            Quantity = 150u,
        }, header);

        var claimId = Guid.Parse(response.ClaimId.Value);
        var claim = await WaitForClaim(Guid.Parse(productionId.StreamId.Value), Guid.Parse(consumptionId.StreamId.Value));

        //Assert
        claim.Should().NotBeNull();
        claim!.State.Should().Be(ClaimState.Claimed);
    }

    private async Task<Claim?> WaitForClaim(Guid prodCertId, Guid consCertId)
    {
        await using var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        var startedAt = DateTime.UtcNow;
        while (DateTime.UtcNow - startedAt < TimeSpan.FromMinutes(1))
        {
            // Verify slice created in database
            var claim = await connection.QuerySingleOrDefaultAsync<Claim>(
                @"SELECT c.*
                  FROM claims c
                  INNER JOIN slices s_prod
                    ON c.production_slice_id = s_prod.id
                  INNER JOIN slices s_cons
                    ON c.consumption_slice_id = s_cons.id
                WHERE s_prod.certificateId = @prodCertId AND s_cons.certificateId = @consCertId",
                new { prodCertId, consCertId });
            if (claim is not null)
                return claim;
            await Task.Delay(1000);
        }
        return null;
    }

}
