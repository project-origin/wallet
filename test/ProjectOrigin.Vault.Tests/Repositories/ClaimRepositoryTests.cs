using AutoFixture;
using FluentAssertions;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Repositories;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ProjectOrigin.Vault.Tests.TestClassFixtures;

namespace ProjectOrigin.Vault.Tests.Repositories;

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
        var result = await _claimRepository.QueryClaims(new QueryClaimsFilter
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
        var result = await _claimRepository.QueryClaims(new QueryClaimsFilter
        {
            Owner = owner
        });

        // Assert
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(48);
        result.Items.Sum(x => x.Quantity).Should().Be(16500);
        result.Items.First().ClaimId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ClaimQuery_Filter_StartDate()
    {
        // Arrange
        var owner = _fixture.Create<string>();
        var startDate = new DateTimeOffset(2023, 7, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateClaimsAndCerts(owner, 48, startDate);

        // Act
        var result = await _claimRepository.QueryClaims(new QueryClaimsFilter()
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
        var result = await _claimRepository.QueryClaims(new QueryClaimsFilter()
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
        var result = await _claimRepository.QueryClaims(new QueryClaimsFilter()
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

    [Theory]
    [InlineData("2020-06-08T12:00:00Z", "2020-06-10T12:00:00Z", 10, 0, 10, 48)]
    [InlineData("2020-06-08T12:00:00Z", "2020-06-10T12:00:00Z", 10, 20, 10, 48)]
    [InlineData("2020-06-08T12:00:00Z", "2020-06-10T12:00:00Z", 10, 40, 8, 48)]
    public async Task QueryClaims_Pagination(string from, string to, int take, int skip, int numberOfResults, int total)
    {
        // Arrange
        var owner = _fixture.Create<string>();
        var startDate = new DateTimeOffset(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateClaimsAndCerts(owner, 31 * 24, startDate);

        // Act
        var result = await _claimRepository.QueryClaims(new QueryClaimsFilter()
        {
            Owner = owner,
            Start = DateTimeOffset.Parse(from),
            End = DateTimeOffset.Parse(to),
            Limit = take,
            Skip = skip,
        });

        // Assert
        result.Items.Should().HaveCount(numberOfResults);
        result.Offset.Should().Be(skip);
        result.Limit.Should().Be(take);
        result.Count.Should().Be(numberOfResults);
        result.TotalCount.Should().Be(total);
    }

    [Fact]
    public async Task QueryClaims_UpdateSinceNull()
    {
        // Arrange
        var owner = _fixture.Create<string>();
        var startDate = new DateTimeOffset(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateClaimsAndCerts(owner, 1, startDate);

        // Act
        var result = await _claimRepository.QueryClaimsCursor(new QueryClaimsFilterCursor()
        {
            Owner = owner,
            Start = startDate,
            End = startDate.AddHours(4),
            Limit = 3,
        });

        // Assert
        result.Items.Should().HaveCount(1);
        result.Limit.Should().Be(3);
        result.Count.Should().Be(1);
        result.TotalCount.Should().Be(1);
        result.Items.Should().BeInAscendingOrder(x => x.UpdatedAt);
    }

    [Fact]
    public async Task QueryClaims_Cursor()
    {
        // Arrange
        var owner = _fixture.Create<string>();
        var numberOfClaims = 31 * 24;
        var startDate = new DateTimeOffset(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateClaimsAndCerts(owner, numberOfClaims, startDate, 10);

        // Act
        var result = await _claimRepository.QueryClaimsCursor(new QueryClaimsFilterCursor()
        {
            Owner = owner,
            UpdatedSince = startDate,
            Limit = 3,
        });

        // Assert
        result.Items.Should().HaveCount(3);
        result.Limit.Should().Be(3);
        result.Count.Should().Be(3);
        result.TotalCount.Should().Be(numberOfClaims);
        result.Items.Should().BeInAscendingOrder(x => x.UpdatedAt);

        var cursor = result.Items.Last().UpdatedAt;
        var result2 = await _claimRepository.QueryClaimsCursor(new QueryClaimsFilterCursor()
        {
            Owner = owner,
            UpdatedSince = cursor,
            Limit = 3,
        });
        result2.Items.First().UpdatedAt.Should().BeAfter(cursor);
    }

    [Theory]
    [InlineData("2020-06-08T12:00:00Z", "2020-06-12T12:00:00Z", TimeAggregate.Total, "Europe/Copenhagen", 2, 0, 1, 1)]
    [InlineData("2020-06-08T12:00:00Z", "2020-06-12T12:00:00Z", TimeAggregate.Day, "Europe/Copenhagen", 2, 2, 2, 5)]
    [InlineData("2020-06-08T00:00:00Z", "2020-06-12T00:00:00Z", TimeAggregate.Day, "Europe/Copenhagen", 2, 4, 1, 5)]
    [InlineData("2020-06-01T00:00:00Z", "2020-06-03T12:00:00Z", TimeAggregate.Day, "Europe/Copenhagen", 2, 0, 2, 3)]
    [InlineData("2020-06-01T00:00:00Z", "2020-06-03T12:00:00Z", TimeAggregate.Day, "America/Toronto", 2, 0, 2, 4)]
    public async Task QueryAggregatedClaims_Pagination(string from, string to, TimeAggregate aggregate, string timeZone, int take, int skip, int numberOfResults, int total)
    {
        // Arrange
        var owner = _fixture.Create<string>();
        var startDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateClaimsAndCerts(owner, 365 * 24, startDate);

        // Act
        var result = await _claimRepository.QueryAggregatedClaims(new QueryAggregatedClaimsFilter()
        {
            Owner = owner,
            Start = DateTimeOffset.Parse(from),
            End = DateTimeOffset.Parse(to),
            Limit = take,
            Skip = skip,
            TimeAggregate = aggregate,
            TimeZone = timeZone
        });

        //assert
        result.Items.Should().HaveCount(numberOfResults);
        result.Offset.Should().Be(skip);
        result.Limit.Should().Be(take);
        result.Count.Should().Be(numberOfResults);
        result.TotalCount.Should().Be(total);
    }

    private async Task CreateClaimsAndCerts(string owner, int numberOfClaims, DateTimeOffset startDate, int delay = 0)
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
            await Task.Delay(delay);
        }
    }
}
