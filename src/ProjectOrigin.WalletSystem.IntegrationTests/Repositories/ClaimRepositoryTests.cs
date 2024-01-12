using AutoFixture;
using Dapper;
using FluentAssertions;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.Server.Extensions;
using Xunit;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server.Activities.Exceptions;
using System.Text;
using ProjectOrigin.WalletSystem.IntegrationTests.TestExtensions;

namespace ProjectOrigin.WalletSystem.IntegrationTests.Repositories;

public class ClaimRepositoryTests : AbstractRepositoryTests
{
    //private readonly CertificateRepository _certRepository;
    private readonly ClaimRepository _claimRepository;

    public ClaimRepositoryTests(PostgresDatabaseFixture dbFixture) : base(dbFixture)
    {
        _claimRepository = new ClaimRepository(_connection);
    }

    [Fact]
    public async Task Claims_InsertSetState_GetResult()
    {
        // Arrange
        var certRepository = new CertificateRepository(_connection);
        var registry = _fixture.Create<string>();
        var certificate = await CreateCertificate(registry);
        var owner = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);
        var endpoint = await CreateWalletEndpoint(wallet);
        var slice = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = 1,
            RegistryName = registry,
            CertificateId = certificate.Id,
            Quantity = 150,
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };
        await certRepository.InsertWalletSlice(slice);
        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            ConsumptionSliceId = slice.Id,
            ProductionSliceId = slice.Id,
            State = ClaimState.Created
        };

        // Act
        await _claimRepository.InsertClaim(claim);
        await _claimRepository.SetClaimState(claim.Id, ClaimState.Claimed);
        var insertedClaim = await _claimRepository.GetClaim(claim.Id);

        // Assert
        insertedClaim.Should().BeEquivalentTo(claim with { State = ClaimState.Claimed });
    }

    [Fact]
    public async Task Claims_Query_Empty()
    {
        // Arrange
        var owner = _fixture.Create<string>();

        // Act
        var result = await _claimRepository.QueryClaims(new ClaimFilter
        {
            Owner = owner
        });

        // Assert
        result.Items.Should().NotBeNull();
        result.Items.Should().BeEmpty();
    }


    [Fact]
    public async Task Claims_Query_Success()
    {
        // Arrange
        var owner = _fixture.Create<string>();
        var startDate = new DateTimeOffset(2023, 7, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateClaimsAndCerts(owner, 48, startDate);

        // Act
        var result = await _claimRepository.QueryClaims(new ClaimFilter
        {
            Owner = owner
        });

        // Assert
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(48);
        result.Items.Sum(x => x.Quantity).Should().Be(16500);
    }

    [Fact]
    public async Task ClaimQuery_Filter_StartDate()
    {
        // Arrange
        var owner = _fixture.Create<string>();
        var startDate = new DateTimeOffset(2023, 7, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateClaimsAndCerts(owner, 48, startDate);

        // Act
        var result = await _claimRepository.QueryClaims(new ClaimFilter()
        {
            Owner = owner,
            Start = startDate.AddHours(48 - 4)
        });

        // Assert
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(4);
        result.Items.Sum(x => x.Quantity).Should().Be(1300);
    }

    [Fact]
    public async Task ClaimQuery_Filter_EndDate()
    {
        // Arrange
        var owner = _fixture.Create<string>();
        var startDate = new DateTimeOffset(2023, 7, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateClaimsAndCerts(owner, 48, startDate);

        // Act
        var result = await _claimRepository.QueryClaims(new ClaimFilter()
        {
            Owner = owner,
            End = startDate.AddHours(4)
        });

        // Assert
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(4);
        result.Items.Sum(x => x.Quantity).Should().Be(1200);
    }

    [Fact]
    public async Task ClaimQuery_Filter_StartRange()
    {
        // Arrange
        var owner = _fixture.Create<string>();
        var startDate = new DateTimeOffset(2023, 7, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateClaimsAndCerts(owner, 48, startDate);

        // Act
        var result = await _claimRepository.QueryClaims(new ClaimFilter()
        {
            Owner = owner,
            Start = startDate.AddHours(10),
            End = startDate.AddHours(15)
        });

        // Assert
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(5);
        result.Items.Sum(x => x.Quantity).Should().Be(1750L);
    }

    private async Task CreateClaimsAndCerts(string owner, int numberOfClaims, DateTimeOffset startDate)
    {
        var certRepository = new CertificateRepository(_connection);

        var registry = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);
        var endpoint = await CreateWalletEndpoint(wallet);

        var position = 1;
        for (int i = 0; i < numberOfClaims; i++)
        {
            var conCert = await CreateCertificate(registry, GranularCertificateType.Consumption, startDate.AddHours(i), endDate: startDate.AddHours(i + 1));
            var conSlice = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = endpoint.Id,
                WalletEndpointPosition = position++,
                RegistryName = registry,
                CertificateId = conCert.Id,
                Quantity = 150 + 100 * (i % 5),
                RandomR = _fixture.Create<byte[]>(),
                State = WalletSliceState.Claimed
            };
            await certRepository.InsertWalletSlice(conSlice);

            var prodCert = await CreateCertificate(registry, GranularCertificateType.Production, startDate.AddHours(i), endDate: startDate.AddHours(i + 1));
            var prodSlice = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = endpoint.Id,
                WalletEndpointPosition = position++,
                RegistryName = registry,
                CertificateId = prodCert.Id,
                Quantity = 150 + 100 * (i % 5),
                RandomR = _fixture.Create<byte[]>(),
                State = WalletSliceState.Claimed
            };
            await certRepository.InsertWalletSlice(prodSlice);

            var claim = new Claim
            {
                Id = Guid.NewGuid(),
                ConsumptionSliceId = conSlice.Id,
                ProductionSliceId = prodSlice.Id,
                State = ClaimState.Claimed
            };
            await _claimRepository.InsertClaim(claim);
        }
    }
}
