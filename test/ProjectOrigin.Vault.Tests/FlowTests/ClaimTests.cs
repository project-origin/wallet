using ProjectOrigin.Vault.Tests.TestClassFixtures;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using ProjectOrigin.PedersenCommitment;
using FluentAssertions;
using System.Linq;
using System.Net.Http.Headers;
using ProjectOrigin.Vault.Services.REST.v1;

namespace ProjectOrigin.Vault.Tests.FlowTests;

public class ClaimTests : AbstractFlowTests
{
    public ClaimTests(
            TestServerFixture<Startup> serverFixture,
            PostgresDatabaseFixture dbFixture,
            InMemoryFixture inMemoryFixture,
            JwtTokenIssuerFixture jwtTokenIssuerFixture,
            StampAndRegistryFixture stampAndRegistryFixture,
            ITestOutputHelper outputHelper)
            : base(
                  serverFixture,
                  dbFixture,
                  inMemoryFixture,
                  jwtTokenIssuerFixture,
                  outputHelper,
                  stampAndRegistryFixture)
    {
    }

    [Fact]
    public async Task Claim150_FromTwoLargerSlices_Success()
    {
        //Arrange
        var position = 1;

        var client = _serverFixture.CreateHttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtTokenIssuer.GenerateRandomToken());

        var wallet = await client.CreateWallet();
        var endpoint = await client.CreateWalletEndpoint(wallet.WalletId);

        var productionId = await IssueCertificateToEndpoint(endpoint.WalletReference, Electricity.V1.GranularCertificateType.Production, new SecretCommitmentInfo(200), position++);
        var consumptionId = await IssueCertificateToEndpoint(endpoint.WalletReference, Electricity.V1.GranularCertificateType.Consumption, new SecretCommitmentInfo(300), position++);

        await client.GetCertificatesWithTimeout(2, TimeSpan.FromMinutes(1));

        //Act
        var response = await client.CreateClaim(
            consumptionId,
            productionId,
            150u);

        //Assert
        var queryClaims = await Timeout(async () =>
        {
            var queryClaims = await client.GetAsync("v1/claims").ParseJson<ResultList<Claim, PageInfo>>();
            queryClaims.Result.Should().NotBeEmpty();
            return queryClaims;
        }, TimeSpan.FromMinutes(3));

        queryClaims.Result.Should().HaveCount(1);
        queryClaims.Result.Single().ConsumptionCertificate.FederatedStreamId.Should().BeEquivalentTo(consumptionId);
        queryClaims.Result.Single().ProductionCertificate.FederatedStreamId.Should().BeEquivalentTo(productionId);
    }
}
