using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System;
using System.Threading.Tasks;
using Xunit;
using ProjectOrigin.WalletSystem.Server.Models;
using Xunit.Abstractions;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.WalletSystem.IntegrationTests.TestExtensions;
using ProjectOrigin.Common.V1;
using FluentAssertions;
using System.Linq;

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

    private async Task<FederatedStreamId> IssueCertIntoWalletEndpoint(WalletEndpoint endpoint, uint issuedAmount, Electricity.V1.GranularCertificateType type)
    {
        var prodCommitment = new SecretCommitmentInfo(issuedAmount);
        var position = 1;
        var issuedEvent = await _registryFixture.IssueCertificate(type, prodCommitment, endpoint.PublicKey.Derive(position).GetPublicKey());
        await _dbFixture.InsertSlice(endpoint, position, issuedEvent, prodCommitment);
        return issuedEvent.CertificateId;
    }

    [Fact]
    public async Task Claim150_FromTwoLargerSlices_Success()
    {
        //Arrange
        var client = new V1.WalletService.WalletServiceClient(_grpcFixture.Channel);

        var (owner, header) = GenerateUserHeader();
        var senderEndpoint = await _dbFixture.CreateReceiveEndpoint(owner);

        var consumptionId = await IssueCertIntoWalletEndpoint(senderEndpoint, 300, Electricity.V1.GranularCertificateType.Consumption);
        var productionId = await IssueCertIntoWalletEndpoint(senderEndpoint, 200, Electricity.V1.GranularCertificateType.Production);

        //Act
        var response = await client.ClaimCertificatesAsync(new V1.ClaimRequest()
        {
            ConsumptionCertificateId = consumptionId,
            ProductionCertificateId = productionId,
            Quantity = 150u,
        }, header);

        //Assert
        var queryClaims = await Timeout(async () =>
        {
            var queryClaims = await client.QueryClaimsAsync(new V1.ClaimQueryRequest(), header);
            queryClaims.Claims.Should().NotBeEmpty();
            return queryClaims;
        }, TimeSpan.FromMinutes(3));

        queryClaims.Claims.Should().HaveCount(1);
        queryClaims.Claims.Single().ConsumptionCertificate.FederatedId.Should().BeEquivalentTo(consumptionId);
        queryClaims.Claims.Single().ProductionCertificate.FederatedId.Should().BeEquivalentTo(productionId);
    }

    private static async Task<T> Timeout<T>(Func<Task<T>> func, TimeSpan timeout)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                return await func();
            }
            catch (Exception)
            {
                await Task.Delay(1000);
            }
        }
        throw new TimeoutException();
    }
}
