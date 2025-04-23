using System;
using System.Threading.Tasks;
using Xunit;
using ProjectOrigin.PedersenCommitment;
using FluentAssertions;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.Vault.Services.REST.v1;
using Claim = ProjectOrigin.Vault.Services.REST.v1.Claim;
using System.Net.Http.Json;
using Dapper;
using Npgsql;
using ProjectOrigin.Vault.Models;
using GranularCertificateType = ProjectOrigin.Electricity.V1.GranularCertificateType;

namespace ProjectOrigin.Vault.Tests.FlowTests;

[Collection(WalletSystemTestCollection.CollectionName)]
public class ClaimTests : AbstractFlowTests
{
    public ClaimTests(WalletSystemTestFixture walletTestFixture) : base(walletTestFixture)
    {
    }

    [Fact]
    public async Task ClaimFailsOnExpiredCertificates_SlicesRemainNonAvailable()
    {
        var position = 1;
        var sixtyDaysAgo = DateTimeOffset.UtcNow.AddDays(
            -WalletTestFixture.DaysBeforeCertificatesExpire!.Value);

        var startDate = sixtyDaysAgo.AddHours(-1);
        var endDate = sixtyDaysAgo;

        var client = WalletTestFixture.ServerFixture.CreateHttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer",
                WalletTestFixture.JwtTokenIssuerFixture.GenerateRandomToken());

        var wallet = await client.CreateWallet();
        var endpoint = await client.CreateWalletEndpoint(wallet.WalletId);

        var productionId = await IssueCertificateToEndpoint(
            endpoint.WalletReference,
            Electricity.V1.GranularCertificateType.Production,
            new SecretCommitmentInfo(200),
            position++,
            startDate,
            endDate);

        var consumptionId = await IssueCertificateToEndpoint(
            endpoint.WalletReference,
            Electricity.V1.GranularCertificateType.Consumption,
            new SecretCommitmentInfo(300),
            position++,
            startDate,
            endDate);

        await client.GetCertificatesWithTimeout(2, TimeSpan.FromMinutes(1));

        await client.CreateClaim(consumptionId, productionId, 150u);

        Guid prodCertGuid = productionId.StreamId;
        Guid consCertGuid = consumptionId.StreamId;

        await Timeout(async () =>
        {
            const string sql = """
                               SELECT c.state                    AS claim_state,
                                      prod.state                AS prod_slice_state,
                                      cons.state               AS cons_slice_state
                               FROM   claims          c
                               JOIN wallet_slices prod ON prod.id = c.production_slice_id
                               JOIN wallet_slices cons ON cons.id = c.consumption_slice_id
                               WHERE  prod.certificate_id = @prodCert
                                 AND  cons.certificate_id = @consCert;
                               """;

            await using var conn =
                new NpgsqlConnection(WalletTestFixture.DbFixture.ConnectionString);

            var row = await conn.QuerySingleOrDefaultAsync(sql,
                new { prodCert = prodCertGuid, consCert = consCertGuid });

            if (row == null)
                return false;

            ((ClaimState)row.claim_state).Should().Be(ClaimState.Rejected,
                "registry rejects transactions on expired certificates");

            ((WalletSliceState)row.prod_slice_state).Should().Be(WalletSliceState.Available,
                "production slice was not rolled back to Available");
            ((WalletSliceState)row.cons_slice_state).Should().Be(WalletSliceState.Available,
                "consumption slice was not rolled back to Available");

            return true;
        }, TimeSpan.FromMinutes(3));
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

    [Fact]
    public async Task WhenClaimingMoreProductionThanPossible_BadRequest()
    {
        var position = 1;
        var endDate = DateTimeOffset.UtcNow;
        var startDate = endDate.AddHours(-1);

        var client = WalletTestFixture.ServerFixture.CreateHttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WalletTestFixture.JwtTokenIssuerFixture.GenerateRandomToken());

        var wallet = await client.CreateWallet();
        var endpoint = await client.CreateWalletEndpoint(wallet.WalletId);

        var productionId = await IssueCertificateToEndpoint(endpoint.WalletReference, Electricity.V1.GranularCertificateType.Production, new SecretCommitmentInfo(200), position++, startDate, endDate);
        var consumptionId = await IssueCertificateToEndpoint(endpoint.WalletReference, Electricity.V1.GranularCertificateType.Consumption, new SecretCommitmentInfo(500), position++, startDate, endDate);

        await client.GetCertificatesWithTimeout(2, TimeSpan.FromMinutes(1));

        var request = new ClaimRequest
        {
            ConsumptionCertificateId = consumptionId,
            ProductionCertificateId = productionId,
            Quantity = 400u
        };
        var response = await client.PostAsync("v1/claims", JsonContent.Create(request));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WhenClaimingMoreConsumptionThanPossible_BadRequest()
    {
        var position = 1;
        var endDate = DateTimeOffset.UtcNow;
        var startDate = endDate.AddHours(-1);

        var client = WalletTestFixture.ServerFixture.CreateHttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WalletTestFixture.JwtTokenIssuerFixture.GenerateRandomToken());

        var wallet = await client.CreateWallet();
        var endpoint = await client.CreateWalletEndpoint(wallet.WalletId);

        var productionId = await IssueCertificateToEndpoint(endpoint.WalletReference, Electricity.V1.GranularCertificateType.Production, new SecretCommitmentInfo(500), position++, startDate, endDate);
        var consumptionId = await IssueCertificateToEndpoint(endpoint.WalletReference, Electricity.V1.GranularCertificateType.Consumption, new SecretCommitmentInfo(200), position++, startDate, endDate);

        await client.GetCertificatesWithTimeout(2, TimeSpan.FromMinutes(1));

        var request = new ClaimRequest
        {
            ConsumptionCertificateId = consumptionId,
            ProductionCertificateId = productionId,
            Quantity = 400u
        };
        var response = await client.PostAsync("v1/claims", JsonContent.Create(request));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WhenTryingToClaimUnknownCertificate_BadRequest()
    {
        var position = 1;
        var endDate = DateTimeOffset.UtcNow;
        var startDate = endDate.AddHours(-1);

        var client = WalletTestFixture.ServerFixture.CreateHttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WalletTestFixture.JwtTokenIssuerFixture.GenerateRandomToken());

        var wallet = await client.CreateWallet();
        var endpoint = await client.CreateWalletEndpoint(wallet.WalletId);

        var consumptionId = await IssueCertificateToEndpoint(endpoint.WalletReference, Electricity.V1.GranularCertificateType.Consumption, new SecretCommitmentInfo(300), position++, startDate, endDate);

        await client.GetCertificatesWithTimeout(1, TimeSpan.FromMinutes(1));

        var request = new ClaimRequest
        {
            ConsumptionCertificateId = consumptionId,
            ProductionCertificateId = consumptionId with { StreamId = Guid.NewGuid() },
            Quantity = 400u
        };
        var response = await client.PostAsync("v1/claims", JsonContent.Create(request));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
