using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Newtonsoft.Json;
using Npgsql;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Repositories;
using ProjectOrigin.Vault.Services.REST.v1;
using Xunit;
using PageInfo = ProjectOrigin.Vault.Services.REST.v1.PageInfo;

namespace ProjectOrigin.Vault.Tests;

[Collection(WalletSystemTestCollection.CollectionName)]
public class ExpireTests 
{
    private readonly WalletSystemTestFixture _walletTestFixture;
    private readonly Fixture _fixture;

    public ExpireTests(WalletSystemTestFixture walletTestFixture)
    {
        _walletTestFixture = walletTestFixture;
        _fixture = new Fixture();
    }

    [Theory]
    [InlineData(GranularCertificateType.Production)]
    [InlineData(GranularCertificateType.Consumption)]
    public async Task ExpireCertificates(GranularCertificateType type)
    {
        var owner = _fixture.Create<string>();
        var someOwnerName = _fixture.Create<string>();
        var walletEndpoint = await CreateWalletAndEndpoint(owner);
        var certToExpire = await AddCertificate(walletEndpoint,
            type,
            startDate: DateTimeOffset.UtcNow.AddDays(-_walletTestFixture.DaysBeforeCertificatesExpire).AddHours(-1),
            endDate: DateTimeOffset.UtcNow.AddDays(-_walletTestFixture.DaysBeforeCertificatesExpire));
        var cert = await AddCertificate(walletEndpoint,
            type,
            startDate: DateTimeOffset.UtcNow.AddHours(-1),
            endDate: DateTimeOffset.UtcNow);

        await Task.Delay(TimeSpan.FromSeconds(_walletTestFixture.ExpireCertificatesIntervalInSeconds));

        var httpClient = _walletTestFixture.CreateAuthenticatedHttpClient(owner, someOwnerName);

        var res = await httpClient.GetAsync($"v1/certificates");
        var content = JsonConvert.DeserializeObject<ResultList<GranularCertificate, PageInfo>>(await res.Content.ReadAsStringAsync());

        content.Should().NotBeNull();
        content.Result.Count().Should().Be(1);
        content.Result.First().FederatedStreamId.StreamId.Should().Be(cert.Id);
    }

    private async Task<WalletEndpoint> CreateWalletAndEndpoint(string owner)
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

            return walletEndpoint;
        }
    }

    private async Task<Certificate> AddCertificate(WalletEndpoint walletEndpoint, GranularCertificateType type, DateTimeOffset startDate, DateTimeOffset endDate)
    {
        using (var connection = new NpgsqlConnection(_walletTestFixture.DbFixture.ConnectionString))
        {
            var regName = _fixture.Create<string>();
            var certificateRepository = new CertificateRepository(connection);

            var attributes = new List<CertificateAttribute>();
            if (type == GranularCertificateType.Production)
            {
                attributes.Add(new CertificateAttribute { Key = "TechCode", Value = "T070000", Type = CertificateAttributeType.ClearText });
                attributes.Add(new CertificateAttribute { Key = "FuelCode", Value = "F00000000", Type = CertificateAttributeType.ClearText });
                attributes.Add(new CertificateAttribute { Key = "AssetId", Value = "571234567890123456", Type = CertificateAttributeType.Hashed });
            }
            else
            {
                attributes.Add(new CertificateAttribute { Key = "AssetId", Value = "571234567890123456", Type = CertificateAttributeType.Hashed });
            }

            var certificate1 = new Certificate
            {
                Id = Guid.NewGuid(),
                RegistryName = regName,
                StartDate = startDate,
                EndDate = endDate,
                GridArea = "DK1",
                CertificateType = type,
                Attributes = attributes,
                Withdrawn = false
            };
            await certificateRepository.InsertCertificate(certificate1);

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
                WalletEndpointPosition = 2,
                RegistryName = regName,
                CertificateId = certificate1.Id,
                Quantity = 43,
                RandomR = _fixture.Create<byte[]>(),
                State = WalletSliceState.Available
            };
            await certificateRepository.InsertWalletSlice(slice1);
            await certificateRepository.InsertWalletSlice(slice2);

            return certificate1;
        }
    }
}
