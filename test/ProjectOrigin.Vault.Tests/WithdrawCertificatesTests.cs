using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Npgsql;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.Vault.Tests;

public class WithdrawCertificatesTests : WalletSystemTestsBase, IClassFixture<InMemoryFixture>, IClassFixture<StampAndRegistryFixture>
{
    public WithdrawCertificatesTests(
        TestServerFixture<Startup> serverFixture,
        PostgresDatabaseFixture dbFixture,
        InMemoryFixture inMemoryFixture,
        JwtTokenIssuerFixture jwtTokenIssuerFixture,
        ITestOutputHelper outputHelper,
        StampAndRegistryFixture stampAndRegistryFixture)
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
    public async Task WithdrawCertificate()
    {
        var owner = _fixture.Create<string>();
        var someOwnerName = _fixture.Create<string>();
        var walletClient = CreateAuthenticatedHttpClient(owner, someOwnerName);

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
            RegistryName = RegistryName,
            MeteringPointId = gsrn,
            Certificate = new StampCertificateDto
            {
                Id = certificateId,
                Start = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                End = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
                GridArea = IssuerArea,
                Quantity = 123,
                Type = StampCertificateType.Production,
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

        await Task.Delay(TimeSpan.FromSeconds(30)); //wait for cert to be on registry and sent back to the wallet

        var withdrawResponse = await stampClient.StampWithdrawCertificate(RegistryName, certificateId);

        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            //TODO the query below is wrong
            var withdrawnSlice = await connection.RepeatedlyQueryFirstOrDefaultUntil<WalletSlice>(@"SELECT *
                  FROM wallet_slices
                  WHERE registry_name = @registry
                  AND certificate_id = @certificateId
                  AND state = @state",
                new
                {
                    registry = RegistryName,
                    certificateId,
                    state = WalletSliceState.Claimed
                }, timeLimit: TimeSpan.FromSeconds(45));

            withdrawnSlice.Should().NotBeNull();
            withdrawnSlice.RegistryName.Should().Be(RegistryName);
            withdrawnSlice.CertificateId.Should().Be(certificateId);
        }
    }
}
