using System;
using System.Threading.Tasks;
using Xunit;
using ProjectOrigin.PedersenCommitment;
using FluentAssertions;
using System.Linq;
using System.Net.Http.Headers;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.Vault.Services.REST.v1;
using Claim = ProjectOrigin.Vault.Services.REST.v1.Claim;

namespace ProjectOrigin.Vault.Tests.FlowTests;

[Collection(WalletSystemTestCollection.CollectionName)]
public class ClaimTests : AbstractFlowTests
{
    public ClaimTests(WalletSystemTestFixture walletTestFixture) : base(walletTestFixture)
    {
    }

    [Fact]
    public async Task Claim150_FromTwoLargerSlices_Success()
    {
        //Arrange
        var position = 1;
        var endDate = DateTimeOffset.UtcNow;
        var startDate = endDate.AddHours(-1);

        var client = WalletTestFixture.ServerFixture.CreateHttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WalletTestFixture.JwtTokenIssuerFixture.GenerateRandomToken());

        var wallet = await client.CreateWallet();
        var endpoint = await client.CreateWalletEndpoint(wallet.WalletId);

        var productionId = await IssueCertificateToEndpoint(endpoint.WalletReference, Electricity.V1.GranularCertificateType.Production, new SecretCommitmentInfo(200), position++, startDate, endDate);
        var consumptionId = await IssueCertificateToEndpoint(endpoint.WalletReference, Electricity.V1.GranularCertificateType.Consumption, new SecretCommitmentInfo(300), position++, startDate, endDate);

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

    [Fact]
    public async Task CannotAllocateAfterExpired()
    {
        var sixtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-WalletTestFixture.DaysBeforeCertificatesExpire!.Value);
        var startDate = sixtyDaysAgo.AddHours(-1);
        var endDate = sixtyDaysAgo;

        var client = WalletTestFixture.ServerFixture.CreateHttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WalletTestFixture.JwtTokenIssuerFixture.GenerateRandomToken());

        var wallet = await client.CreateWallet();
        var endpoint = await client.CreateWalletEndpoint(wallet.WalletId);

        var prodCommitmentInfo = new SecretCommitmentInfo(200);
        var prodEvent = await WalletTestFixture.StampAndRegistryFixture.IssueCertificate(
            GranularCertificateType.Production,
            prodCommitmentInfo,
            endpoint.WalletReference.PublicKey.Derive(1).GetPublicKey(),
            startDate,
            endDate,
            null);

        var conCommitmentInfo = new SecretCommitmentInfo(200);
        var conEvent = await WalletTestFixture.StampAndRegistryFixture.IssueCertificate(
            GranularCertificateType.Consumption,
            conCommitmentInfo,
            endpoint.WalletReference.PublicKey.Derive(2).GetPublicKey(),
            startDate,
            endDate,
            null);

        var allocateEventStatus = await WalletTestFixture.StampAndRegistryFixture.AllocateEvent(Guid.NewGuid(),
            prodEvent.CertificateId,
            conEvent.CertificateId,
            prodCommitmentInfo,
            conCommitmentInfo);

        allocateEventStatus.Status.Should().Be(TransactionState.Failed);
        allocateEventStatus.Message.Should().Be("Certificate has expired");
    }
}
