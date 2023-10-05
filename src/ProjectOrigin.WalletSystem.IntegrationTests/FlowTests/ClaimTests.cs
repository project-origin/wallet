using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using ProjectOrigin.PedersenCommitment;
using FluentAssertions;
using System.Linq;

namespace ProjectOrigin.WalletSystem.IntegrationTests.FlowTests;

public class ClaimTests : AbstractFlowTests
{
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
    }

    [Fact]
    public async Task Claim150_FromTwoLargerSlices_Success()
    {
        //Arrange
        var client = new V1.WalletService.WalletServiceClient(_grpcFixture.Channel);
        var position = 1;

        var (owner, header) = GenerateUserHeader();
        var endpoint = await client.CreateWalletDepositEndpointAsync(new V1.CreateWalletDepositEndpointRequest(), header);

        var productionId = await IssueCertificateToEndpoint(endpoint, Electricity.V1.GranularCertificateType.Production, new SecretCommitmentInfo(200), position++);
        var comsumptionId = await IssueCertificateToEndpoint(endpoint, Electricity.V1.GranularCertificateType.Consumption, new SecretCommitmentInfo(300), position++);

        await Timeout(async () =>
        {
            var result = await client.QueryGranularCertificatesAsync(new V1.QueryRequest(), header);
            result.GranularCertificates.Should().HaveCount(2);
            return result.GranularCertificates;
        }, TimeSpan.FromMinutes(1));

        //Act
        var response = await client.ClaimCertificatesAsync(new V1.ClaimRequest()
        {
            ConsumptionCertificateId = comsumptionId,
            ProductionCertificateId = productionId,
            Quantity = 150u,
        }, header);

        //Assert
        var queryClaims = await Timeout(async () =>
        {
            var queryClaims = await client.QueryClaimsAsync(new V1.ClaimQueryRequest(), header);
            queryClaims.Claims.Should().NotBeEmpty();
            return queryClaims;
        }, TimeSpan.FromMinutes(2));

        queryClaims.Claims.Should().HaveCount(1);
        queryClaims.Claims.Single().ConsumptionCertificate.FederatedId.Should().BeEquivalentTo(comsumptionId);
        queryClaims.Claims.Single().ProductionCertificate.FederatedId.Should().BeEquivalentTo(productionId);
    }
}
