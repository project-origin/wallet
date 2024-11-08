using AutoFixture;
using FluentAssertions;
using Npgsql;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Repositories;
using ProjectOrigin.Vault.Services.REST.v1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Claim = ProjectOrigin.Vault.Models.Claim;

namespace ProjectOrigin.Vault.Tests;

[Collection(WalletSystemTestCollection.CollectionName)]
public class ApiTests 
{
    private readonly WalletSystemTestFixture _walletTestFixture;
    private readonly Fixture _fixture;

    public ApiTests(WalletSystemTestFixture walletTestFixture)
    {
        _walletTestFixture = walletTestFixture;
        _fixture = new Fixture();
    }

    [Fact]
    public async Task open_api_specification_not_changed()
    {
        var httpClient = _walletTestFixture.ServerFixture.CreateHttpClient();
        var specificationResponse = await httpClient.GetAsync("swagger/v1/swagger.json");
        var specification = await specificationResponse.Content.ReadAsStringAsync();
        await Verifier.Verify(specification);
    }

    [Fact]
    public async Task can_create_wallet()
    {
        //Arrange
        var owner = _fixture.Create<string>();
        var someOwnerName = _fixture.Create<string>();
        var httpClient = _walletTestFixture.CreateAuthenticatedHttpClient(owner, someOwnerName);

        //Act
        var res = await httpClient.PostAsJsonAsync("v1/wallets", new { });

        //Assert
        var content = await res.Content.ReadAsStringAsync();
        await Verifier.VerifyJson(content);
    }

    [Fact]
    public async Task can_create_wallet_endpoint()
    {
        //Arrange
        var owner = _fixture.Create<string>();
        var someOwnerName = _fixture.Create<string>();
        var httpClient = _walletTestFixture.CreateAuthenticatedHttpClient(owner, someOwnerName);

        var httpResponse = await httpClient.PostAsJsonAsync("v1/wallets", new { });
        var walletResponse = await httpResponse.Content.ReadFromJsonAsync<CreateWalletResponse>();

        //Act
        var res = await httpClient.PostAsJsonAsync($"v1/wallets/{walletResponse!.WalletId}/endpoints", new { });

        //Assert
        var content = await res.Content.ReadAsStringAsync();
        await Verifier.VerifyJson(content)
            .ScrubMember("publicKey");
    }

    [Fact]
    public async Task can_query_certificates()
    {
        //Arrange
        var owner = _fixture.Create<string>();
        var someOwnerName = _fixture.Create<string>();
        var httpClient = _walletTestFixture.CreateAuthenticatedHttpClient(owner, someOwnerName);
        await AddCertificatesToOwner(owner);

        //Act
        var res = await httpClient.GetStringAsync("v1/certificates?sortBy=End&sort=asc");
        var content =
            JsonConvert.DeserializeObject<ResultList<GranularCertificate, PageInfoCursor>>(
                 res);

        content!.Result.Should().BeInAscendingOrder(x => x.End);

        //Assert
        var settings = new VerifySettings();
        settings.ScrubMember("UpdatedAt");
        await Verifier.VerifyJson(res, settings);
    }

    [Fact]
    public async Task query_certificates_sortby_unknown_return_bad_request()
    {
        var owner = _fixture.Create<string>();
        var someOwnerName = _fixture.Create<string>();
        var httpClient = _walletTestFixture.CreateAuthenticatedHttpClient(owner, someOwnerName);
        var res = await httpClient.GetAsync("v1/certificates?sortBy=BADVALUE&sort=asc");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task can_query_single_certificate()
    {
        //Arrange
        var owner = _fixture.Create<string>();
        var someOwnerName = _fixture.Create<string>();
        var httpClient = _walletTestFixture.CreateAuthenticatedHttpClient(owner, someOwnerName);
        await AddCertificatesToOwner(owner);

        //Act
        var res = await httpClient.GetAsync("v1/certificates");

        var content =
            JsonConvert.DeserializeObject<ResultList<GranularCertificate, PageInfoCursor>>(
                await res.Content.ReadAsStringAsync());
        var response = await httpClient.GetStringAsync("v1/certificates/"
                                                 + content!.Result.First().FederatedStreamId.Registry + "/"
                                                 + content.Result.First().FederatedStreamId.StreamId);
        //Assert
        var settings = new VerifySettings();
        settings.ScrubMember("UpdatedAt");
        await Verifier.VerifyJson(response, settings);
    }

    [Fact]
    public async Task can_query_certificates_cursor()
    {
        //Arrange
        var owner = _fixture.Create<string>();
        var someOwnerName = _fixture.Create<string>();
        var httpClient = _walletTestFixture.CreateAuthenticatedHttpClient(owner, someOwnerName);
        await AddCertificatesToOwner(owner);
        var updatedSince = DateTimeOffset.UtcNow.AddMilliseconds(-500).ToUnixTimeSeconds();

        //Act
        var res = await httpClient.GetAsync($"v1/certificates/cursor?UpdatedSince={updatedSince}");
        var content = JsonConvert.DeserializeObject<ResultList<GranularCertificate, PageInfoCursor>>(await res.Content.ReadAsStringAsync());

        //Assert
        var settings = new VerifySettings();
        settings.ScrubMember("UpdatedAt");
        await Verifier.Verify(content, settings);
    }

    [Fact]
    public async Task can_query_certificates_aggregated()
    {
        //Arrange
        var owner = _fixture.Create<string>();
        var someOwnerName = _fixture.Create<string>();
        var httpClient = _walletTestFixture.CreateAuthenticatedHttpClient(owner, someOwnerName);
        await AddCertificatesToOwner(owner);

        //Act
        var res = await httpClient.GetAsync(
            "v1/aggregate-certificates?timeAggregate=hour&timeZone=Europe/Copenhagen");
        var content = JsonConvert.DeserializeObject<ResultList<GranularCertificate, PageInfo>>(await res.Content.ReadAsStringAsync());
        //Assert
        var settings = new VerifySettings();
        settings.ScrubMember("UpdatedAt");
        await Verifier.Verify(content, settings);
    }

    [Fact]
    public async Task can_query_claims()
    {
        //Arrange
        var owner = _fixture.Create<string>();
        var someOwnerName = _fixture.Create<string>();
        var httpClient = _walletTestFixture.CreateAuthenticatedHttpClient(owner, someOwnerName);

        var startDate = DateTimeOffset.Parse("2023-01-01T12:00Z");
        var endDate = DateTimeOffset.Parse("2023-01-01T13:00Z");

        using (var connection = new NpgsqlConnection(_walletTestFixture.DbFixture.ConnectionString))
        {
            var walletRepository = new WalletRepository(connection);
            var wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                Owner = owner,
                PrivateKey = _walletTestFixture.Algorithm.GenerateNewPrivateKey()
            };
            await walletRepository.Create(wallet);

            var walletEndpoint = await walletRepository.CreateWalletEndpoint(wallet.Id);

            var regName = _fixture.Create<string>();
            var certificateRepository = new CertificateRepository(connection);
            var claimRepository = new ClaimRepository(connection);

            var productionCertificate = new Certificate
            {
                Id = Guid.NewGuid(),
                RegistryName = regName,
                StartDate = startDate,
                EndDate = endDate,
                GridArea = "DK1",
                CertificateType = GranularCertificateType.Production,
                Attributes = new List<CertificateAttribute>
                {
                    new() { Key = "AssetId", Value = "571234567890123456", Type = CertificateAttributeType.ClearText },
                    new() { Key = "TechCode", Value = "T070000", Type = CertificateAttributeType.ClearText },
                    new() { Key = "FuelCode", Value = "F00000000", Type = CertificateAttributeType.ClearText },
                },
                Withdrawn = false
            };
            var consumptionCertificate = new Certificate
            {
                Id = Guid.NewGuid(),
                RegistryName = regName,
                StartDate = startDate,
                EndDate = endDate,
                GridArea = "DK1",
                CertificateType = GranularCertificateType.Consumption,
                Attributes = new List<CertificateAttribute>
                {
                    new() { Key = "AssetId", Value = "571234567891234567", Type = CertificateAttributeType.ClearText },
                },
                Withdrawn = false
            };
            await certificateRepository.InsertCertificate(productionCertificate);
            await certificateRepository.InsertCertificate(consumptionCertificate);

            var productionSlice = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = walletEndpoint.Id,
                WalletEndpointPosition = 1,
                RegistryName = regName,
                CertificateId = productionCertificate.Id,
                Quantity = 42,
                RandomR = _fixture.Create<byte[]>(),
                State = WalletSliceState.Available
            };
            var consumptionSlice = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = walletEndpoint.Id,
                WalletEndpointPosition = 1,
                RegistryName = regName,
                CertificateId = consumptionCertificate.Id,
                Quantity = 42,
                RandomR = _fixture.Create<byte[]>(),
                State = WalletSliceState.Available
            };
            await certificateRepository.InsertWalletSlice(productionSlice);
            await certificateRepository.InsertWalletSlice(consumptionSlice);

            var claim = new Claim
            {
                Id = Guid.NewGuid(),
                ProductionSliceId = productionSlice.Id,
                ConsumptionSliceId = consumptionSlice.Id,
                State = ClaimState.Claimed
            };

            await claimRepository.InsertClaim(claim);
        }

        var filterStart = startDate.ToUnixTimeSeconds();
        var filterEnd = endDate.ToUnixTimeSeconds();

        //Act
        var resultWithoutFilters = await httpClient.GetAsync("v1/claims");
        var resultWithFilterStart = await httpClient.GetStringAsync($"v1/claims?start={filterStart}");
        var resultWithFilterEnd = await httpClient.GetStringAsync($"v1/claims?end={filterEnd}");
        var resultWithFilterStartAndEnd =
            await httpClient.GetStringAsync($"v1/claims?start={filterStart}&end={filterEnd}");
        var resultWithFilterOutsideAnyClaims1 =
            await httpClient.GetFromJsonAsync<ResultList<Services.REST.v1.Claim, PageInfo>>(
                $"v1/claims?start={filterEnd}");
        var resultWithFilterOutsideAnyClaims2 =
            await httpClient.GetFromJsonAsync<ResultList<Services.REST.v1.Claim, PageInfo>>(
                $"v1/claims?end={filterStart}");
        var resultWithUpdatedSince =
            await httpClient.GetFromJsonAsync<ResultList<Services.REST.v1.Claim, PageInfoCursor>>(
                $"v1/claims/cursor?UpdatedSince={filterStart}");

        var resultWithoutFiltersJson = await resultWithoutFilters.Content.ReadAsStringAsync();
        var resultWithoutFiltersContent =
            JsonConvert.DeserializeObject<ResultList<Services.REST.v1.Claim, PageInfo>>(
                resultWithoutFiltersJson);
        //Assert
        var settings = new VerifySettings();
        settings.ScrubMember("UpdatedAt");
        await Verifier.Verify(resultWithoutFiltersContent, settings);

        resultWithUpdatedSince.Should().NotBeNull();
        resultWithUpdatedSince!.Result.Should().NotBeEmpty();
        resultWithUpdatedSince.Result.Should().BeInAscendingOrder(x => x.UpdatedAt);
        resultWithoutFiltersJson.Should().Be(resultWithFilterStart);
        resultWithoutFiltersJson.Should().Be(resultWithFilterEnd);
        resultWithoutFiltersJson.Should().Be(resultWithFilterStartAndEnd);
        resultWithFilterOutsideAnyClaims1!.Result.Should().BeEmpty();
        resultWithFilterOutsideAnyClaims2!.Result.Should().BeEmpty();
    }

    private async Task AddCertificatesToOwner(string owner)
    {
        using (var connection = new NpgsqlConnection(_walletTestFixture.DbFixture.ConnectionString))
        {
            var walletRepository = new WalletRepository(connection);
            var wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                Owner = owner,
                PrivateKey = _walletTestFixture.Algorithm.GenerateNewPrivateKey()
            };
            await walletRepository.Create(wallet);

            var walletEndpoint = await walletRepository.CreateWalletEndpoint(wallet.Id);

            var regName = _fixture.Create<string>();
            var certificateRepository = new CertificateRepository(connection);

            var attributes = new List<CertificateAttribute>
            {
                new() { Key = "TechCode", Value = "T070000", Type = CertificateAttributeType.ClearText },
                new() { Key = "FuelCode", Value = "F00000000", Type = CertificateAttributeType.ClearText },
                new() { Key = "AssetId", Value = "571234567890123456", Type = CertificateAttributeType.Hashed },
            };

            var certificate1 = new Certificate
            {
                Id = Guid.NewGuid(),
                RegistryName = regName,
                StartDate = DateTimeOffset.Parse("2023-01-01T12:00Z"),
                EndDate = DateTimeOffset.Parse("2023-01-01T13:00Z"),
                GridArea = "DK1",
                CertificateType = GranularCertificateType.Production,
                Attributes = attributes,
                Withdrawn = false
            };
            var certificate2 = new Certificate
            {
                Id = Guid.NewGuid(),
                RegistryName = regName,
                StartDate = DateTimeOffset.Parse("2023-01-01T13:00Z"),
                EndDate = DateTimeOffset.Parse("2023-01-01T14:00Z"),
                GridArea = "DK1",
                CertificateType = GranularCertificateType.Production,
                Attributes = attributes,
                Withdrawn = false
            };
            await certificateRepository.InsertCertificate(certificate1);
            await certificateRepository.InsertCertificate(certificate2);

            var slice1 = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = walletEndpoint.Id,
                WalletEndpointPosition = 1,
                RegistryName = regName,
                CertificateId = certificate1.Id,
                Quantity = 42,
                RandomR = _fixture.Create<byte[]>(),
                State = WalletSliceState.Available
            };
            var slice2 = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = walletEndpoint.Id,
                WalletEndpointPosition = 1,
                RegistryName = regName,
                CertificateId = certificate1.Id,
                Quantity = 43,
                RandomR = _fixture.Create<byte[]>(),
                State = WalletSliceState.Available
            };
            var slice3 = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = walletEndpoint.Id,
                WalletEndpointPosition = 1,
                RegistryName = regName,
                CertificateId = certificate2.Id,
                Quantity = 44,
                RandomR = _fixture.Create<byte[]>(),
                State = WalletSliceState.Available
            };
            await certificateRepository.InsertWalletSlice(slice1);
            await certificateRepository.InsertWalletSlice(slice2);
            await certificateRepository.InsertWalletSlice(slice3);
        }
    }
}
