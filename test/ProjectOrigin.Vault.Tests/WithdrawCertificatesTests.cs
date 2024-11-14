using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Npgsql;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using Xunit;
using System.Net.Http;
using System.Net.Http.Headers;
using ProjectOrigin.Vault.Services.REST.v1;
using Dapper;

namespace ProjectOrigin.Vault.Tests;

[Collection(DockerTestCollection.CollectionName)]
public class WithdrawCertificatesTests : 
    IClassFixture<JwtTokenIssuerFixture>
{
    private readonly DockerTestFixture _dockerTestFixture;
    private readonly JwtTokenIssuerFixture _jwtTokenIssuerFixture;

    public WithdrawCertificatesTests(DockerTestFixture dockerTestFixture,
        JwtTokenIssuerFixture jwtTokenIssuerFixture)
    {
        _dockerTestFixture = dockerTestFixture;
        _jwtTokenIssuerFixture = jwtTokenIssuerFixture;
    }

    private HttpClient CreateHttpClient(string subject, string name, string[]? scopes = null)
    {
        var client = new HttpClient();
        client.BaseAddress = new UriBuilder("http",
            _dockerTestFixture.WalletContainer.Value.Hostname,
            _dockerTestFixture.WalletContainer.Value.GetMappedPublicPort(_dockerTestFixture.WalletHttpPort),
            _dockerTestFixture.PathBase).Uri;
        var token = _jwtTokenIssuerFixture.GenerateToken(subject, name, scopes);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private HttpClient CreateStampClient()
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri(_dockerTestFixture.StampAndRegistryFixture.StampUrl);
        return client;
    }

    [Theory]
    [InlineData(StampCertificateType.Production)]
    [InlineData(StampCertificateType.Consumption)]
    public async Task WithdrawCertificate(StampCertificateType certificateType)
    {
        var registryName = _dockerTestFixture.StampAndRegistryFixture.RegistryName;
        var issuerArea = _dockerTestFixture.StampAndRegistryFixture.IssuerArea;

        var walletClient = CreateHttpClient(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
        var wResponse = await walletClient.CreateWallet();
        var weResponse = await walletClient.CreateWalletEndpoint(wResponse.WalletId);

        var stampClient = CreateStampClient();
        var rResponse = await stampClient.StampCreateRecipient(new CreateRecipientRequest
        {
            WalletEndpointReference = new StampWalletEndpointReferenceDto
            {
                Version = weResponse.WalletReference.Version,
                Endpoint = weResponse.WalletReference.Endpoint,
                PublicKey = weResponse.WalletReference.PublicKey.Export().ToArray()
            }
        });

        var gsrn = Some.Gsrn();
        var certificateId = Guid.NewGuid();
        var icResponse = await stampClient.StampIssueCertificate(new CreateCertificateRequest
        {
            RecipientId = rResponse.Id,
            RegistryName = registryName,
            MeteringPointId = gsrn,
            Certificate = new StampCertificateDto
            {
                Id = certificateId,
                Start = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                End = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
                GridArea = issuerArea,
                Quantity = 123,
                Type = certificateType,
                ClearTextAttributes = new Dictionary<string, string>
                {
                    { "fuelCode", Some.FuelCode },
                    { "techCode", Some.TechCode }
                },
                HashedAttributes = new List<StampHashedAttribute>
                {
                    new () { Key = "assetId", Value = gsrn },
                    new () { Key = "address", Value = "Some road 1234" }
                }
            }
        });

        await Task.Delay(TimeSpan.FromSeconds(15)); //wait for cert to be on registry and sent back to the wallet

        var withdrawResponse = await stampClient.StampWithdrawCertificate(registryName, certificateId);

        using (var connection = new NpgsqlConnection(_dockerTestFixture.PostgresFixture.GetConnectionString()))
        {
            var certificate = await connection.RepeatedlyQueryFirstOrDefaultUntil<Certificate>(
                @"SELECT id,
                        registry_name as RegistryName,
                        start_date as StartDate,
                        end_date as EndDate,
                        grid_area as GridArea,
                        certificate_type as CertificateType,
                        withdrawn
	                  FROM public.certificates
                      WHERE registry_name = @registry
                      AND id = @certificateId
                      AND withdrawn = true",
                new
                {
                    registry = registryName,
                    certificateId
                }, timeLimit: TimeSpan.FromSeconds(45));

            certificate.Should().NotBeNull();
            certificate.RegistryName.Should().Be(registryName);
            certificate.Id.Should().Be(certificateId);
            certificate.Withdrawn.Should().BeTrue();
        }
    }

    [Theory]
    [InlineData(StampCertificateType.Production)]
    [InlineData(StampCertificateType.Consumption)]
    public async Task WithdrawCertificate_WhenPartOfCertificateWasClaimed_CertificateWithdrawnClaimUnclaimedAndPartAvailable(StampCertificateType certificateType)
    {
        var registryName = _dockerTestFixture.StampAndRegistryFixture.RegistryName;
        var issuerArea = _dockerTestFixture.StampAndRegistryFixture.IssuerArea;

        var walletClient = CreateHttpClient(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
        var wResponse = await walletClient.CreateWallet();
        var weResponse = await walletClient.CreateWalletEndpoint(wResponse.WalletId);

        var stampClient = CreateStampClient();
        var rResponse = await stampClient.StampCreateRecipient(new CreateRecipientRequest
        {
            WalletEndpointReference = new StampWalletEndpointReferenceDto
            {
                Version = weResponse.WalletReference.Version,
                Endpoint = weResponse.WalletReference.Endpoint,
                PublicKey = weResponse.WalletReference.PublicKey.Export().ToArray()
            }
        });

        var prodGsrn = Some.Gsrn();
        var prodCertId = Guid.NewGuid();
        var startDate = DateTimeOffset.UtcNow;
        uint quantity = 123;
        var icProdResponse = await stampClient.StampIssueCertificate(new CreateCertificateRequest
        {
            RecipientId = rResponse.Id,
            RegistryName = registryName,
            MeteringPointId = prodGsrn,
            Certificate = new StampCertificateDto
            {
                Id = prodCertId,
                Start = startDate.ToUnixTimeSeconds(),
                End = startDate.AddHours(1).ToUnixTimeSeconds(),
                GridArea = issuerArea,
                Quantity = quantity,
                Type = StampCertificateType.Production,
                ClearTextAttributes = new Dictionary<string, string>
                {
                    { "fuelCode", Some.FuelCode },
                    { "techCode", Some.TechCode }
                },
                HashedAttributes = new List<StampHashedAttribute>
                {
                    new () { Key = "assetId", Value = prodGsrn },
                    new () { Key = "address", Value = "Some road 1234" }
                }
            }
        });

        var conGsrn = Some.Gsrn();
        var conCertId = Guid.NewGuid();
        var icConResponse = await stampClient.StampIssueCertificate(new CreateCertificateRequest
        {
            RecipientId = rResponse.Id,
            RegistryName = registryName,
            MeteringPointId = conGsrn,
            Certificate = new StampCertificateDto
            {
                Id = conCertId,
                Start = startDate.ToUnixTimeSeconds(),
                End = startDate.AddHours(1).ToUnixTimeSeconds(),
                GridArea = issuerArea,
                Quantity = quantity,
                Type = StampCertificateType.Consumption,
                ClearTextAttributes = new Dictionary<string, string> {},
                HashedAttributes = new List<StampHashedAttribute>
                {
                    new () { Key = "assetId", Value = conGsrn }
                }
            }
        });

        await Task.Delay(TimeSpan.FromSeconds(30)); //wait for cert to be on registry and sent back to the wallet

        var claimResponse = await walletClient.CreateClaim(new FederatedStreamId { Registry = registryName, StreamId = conCertId }, 
            new FederatedStreamId { Registry = registryName, StreamId = prodCertId},
            quantity);

        await Task.Delay(TimeSpan.FromSeconds(30)); //wait for claim

        Guid withdrawnCertificateId;
        if (certificateType == StampCertificateType.Production)
        {
            var withdrawResponse = await stampClient.StampWithdrawCertificate(registryName, prodCertId);
            withdrawnCertificateId = prodCertId;
        }
        else
        {
            var withdrawResponse = await stampClient.StampWithdrawCertificate(registryName, conCertId);
            withdrawnCertificateId = conCertId;
        }

        using (var connection = new NpgsqlConnection(_dockerTestFixture.PostgresFixture.GetConnectionString()))
        {
            var claim = await connection.RepeatedlyQueryFirstOrDefaultUntil<Models.Claim>(
                @"SELECT id,
                        production_slice_id as ProductionSliceId,
                        consumption_slice_id as ConsumptionSliceId,
                        state
                    FROM claims
                    WHERE id = @claimId
                    AND state = @state",
                new
                {
                    claimId = claimResponse.ClaimRequestId,
                    state = ClaimState.Unclaimed
                }, timeLimit: TimeSpan.FromSeconds(45));

            claim.Should().NotBeNull();
            claim.State.Should().Be(ClaimState.Unclaimed);

            var certificate = await connection.QueryFirstOrDefaultAsync<Certificate>(
                @"SELECT id,
                        registry_name as RegistryName,
                        start_date as StartDate,
                        end_date as EndDate,
                        grid_area as GridArea,
                        certificate_type as CertificateType,
                        withdrawn
	                  FROM public.certificates
                      WHERE registry_name = @registry
                      AND id = @certificateId
                      AND withdrawn = true",
                new
                {
                    registry = registryName,
                    certificateId = withdrawnCertificateId
                });

            certificate.Should().NotBeNull();
            certificate!.RegistryName.Should().Be(registryName);
            certificate.Id.Should().Be(withdrawnCertificateId);
            certificate.Withdrawn.Should().BeTrue();
        }

        var certificates = await walletClient.GetCertificates();

        certificates.Result.Should().HaveCount(1);
        certificates.Result.First().FederatedStreamId.StreamId.Should()
            .Be(certificateType == StampCertificateType.Production ? conCertId : prodCertId);

        certificates.Result.First().Quantity.Should().Be(quantity);
    }
}
