using AutoFixture;
using Dapper;
using FluentAssertions;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProjectOrigin.Vault.Extensions;
using Xunit;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using System.Text;
using ProjectOrigin.Vault.Exceptions;

namespace ProjectOrigin.Vault.Tests.Repositories;

public class CertificateRepositoryTests : AbstractRepositoryTests
{
    private readonly CertificateRepository _certRepository;

    public CertificateRepositoryTests(PostgresDatabaseFixture dbFixture) : base(dbFixture)
    {
        _certRepository = new CertificateRepository(_connection);
    }

    [Fact]
    public async Task InsertCertificate_InsertsCertificate()
    {
        // Arrange
        var registry = _fixture.Create<string>();
        var attributes = new List<CertificateAttribute>
        {
            new() { Key = "AssetId", Value = "571234567890123456", Type = CertificateAttributeType.ClearText },
            new() { Key = "TechCode", Value = "T070000", Type = CertificateAttributeType.ClearText },
            new() { Key = "FuelCode", Value = "F00000000", Type = CertificateAttributeType.ClearText },
        };
        var certificate = new Certificate
        {
            Id = Guid.NewGuid(),
            RegistryName = registry,
            StartDate = DateTimeOffset.Now.ToUtcTime(),
            EndDate = DateTimeOffset.Now.AddDays(1).ToUtcTime(),
            GridArea = "DK1",
            CertificateType = GranularCertificateType.Production,
            Attributes = attributes,
            Withdrawn = false
        };

        // Act
        await _certRepository.InsertCertificate(certificate);

        // Assert
        var insertedCertificate = await _certRepository.GetCertificate(certificate.RegistryName, certificate.Id);
        insertedCertificate.Should().BeEquivalentTo(certificate);
    }

    [Fact]
    public async Task GetCertificate_ReturnsCertificate()
    {
        // Arrange
        var registry = _fixture.Create<string>();
        var certificate = await CreateCertificate(registry);

        // Act
        var result = await _certRepository.GetCertificate(registry, certificate.Id);

        // Assert
        result.Should().BeEquivalentTo(certificate);
    }

    [Fact]
    public async Task WithdrawCertificate()
    {
        var registry = _fixture.Create<string>();
        var certificate1 = await CreateCertificate(registry);
        var certificate2 = await CreateCertificate(registry);

        await _certRepository.WithdrawCertificate(registry, certificate1.Id);

        var result1 = await _certRepository.GetCertificate(registry, certificate1.Id);
        var result2 = await _certRepository.GetCertificate(registry, certificate2.Id);

        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1!.Withdrawn.Should().BeTrue();
        result2!.Withdrawn.Should().BeFalse();
    }

    [Fact]
    public async Task WithdrawCertificate_WhenAlreadyWithdrawn()
    {
        var registry = _fixture.Create<string>();
        var certificate1 = await CreateCertificate(registry);

        await _certRepository.WithdrawCertificate(registry, certificate1.Id);
        await _certRepository.WithdrawCertificate(registry, certificate1.Id);

        var result1 = await _certRepository.GetCertificate(registry, certificate1.Id);

        result1.Should().NotBeNull();
        result1!.Withdrawn.Should().BeTrue();
    }

    [Fact]
    public async Task GetCertificatesAndAggregatedCertificates_ShouldNotReturnWithdrawnCertificates()
    {
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
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };

        await _certRepository.InsertWalletSlice(slice);
        await _certRepository.WithdrawCertificate(registry, certificate.Id);

        var queryResult1 = await _certRepository.QueryCertificates(new QueryCertificatesFilterCursor
        {
            Owner = owner,
            UpdatedSince = null
        });
        queryResult1.Items.Should().BeEmpty();

        var queryResult2 = await _certRepository.QueryAvailableCertificates(new QueryCertificatesFilter
        {
            Owner = owner
        });
        queryResult2.Items.Should().BeEmpty();

        var aggregated = await _certRepository.QueryAggregatedAvailableCertificates(new QueryAggregatedCertificatesFilter
        {
            Owner = owner,
            TimeAggregate = TimeAggregate.Day,
            TimeZone = "Europe/Copenhagen"
        });
        aggregated.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetClaimedSlicesOfCertificate()
    {
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
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };
        var claimedSlice = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = 2,
            RegistryName = registry,
            CertificateId = certificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Claimed
        };

        await _certRepository.InsertWalletSlice(slice);
        await _certRepository.InsertWalletSlice(claimedSlice);

        var claimedSlices = await _certRepository.GetClaimedSlicesOfCertificate(registry, certificate.Id);

        claimedSlices.Should().HaveCount(1);
        claimedSlices.First().Should().BeEquivalentTo(claimedSlice, options => options.Excluding(x => x.UpdatedAt));
    }

    [Fact]
    public async Task CreateSlice_InsertsSlice()
    {
        // Arrange
        var endpointPosition = 1;
        var registry = _fixture.Create<string>();
        var certificate = await CreateCertificate(registry);
        var wallet = await CreateWallet(registry);
        var endpoint = await CreateWalletEndpoint(wallet);
        var slice = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = endpointPosition,
            RegistryName = registry,
            CertificateId = certificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };

        // Act
        await _certRepository.InsertWalletSlice(slice);

        // Assert
        var insertedSlice =
            await _connection.QueryFirstOrDefaultAsync<WalletSlice>("SELECT * FROM wallet_slices WHERE id = @id",
                new { slice.Id });
        insertedSlice.Should().NotBeNull();
        insertedSlice.Should().BeEquivalentTo(slice, options => options.Excluding(x => x.UpdatedAt));
        insertedSlice!.UpdatedAt.Hour.Should().Be(DateTimeOffset.UtcNow.Hour);
        insertedSlice.UpdatedAt.Day.Should().Be(DateTimeOffset.UtcNow.Day);
        insertedSlice.UpdatedAt.Year.Should().Be(DateTimeOffset.UtcNow.Year);
        insertedSlice.UpdatedAt.Month.Should().Be(DateTimeOffset.UtcNow.Month);
    }

    [Theory]
    [InlineData(-10, true)]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public async Task ExpireSlices(int addHoursToStartDate, bool expired)
    {
        var daysBeforeCertificatesExpire = DateTimeOffset.UtcNow.AddDays(-60);

        var registry = _fixture.Create<string>();
        var startDate = daysBeforeCertificatesExpire.AddHours(addHoursToStartDate);
        var certificate = await CreateCertificate(registry, startDate: startDate, endDate: startDate.AddHours(1));
        var wallet = await CreateWallet(registry);
        var endpoint = await CreateWalletEndpoint(wallet);
        var slice = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = 1,
            RegistryName = registry,
            CertificateId = certificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };

        await _certRepository.InsertWalletSlice(slice);

        await _certRepository.ExpireSlices(daysBeforeCertificatesExpire);

        var sliceDb = await _certRepository.GetWalletSlice(slice.Id);

        if (expired)
        {
            sliceDb.State.Should().Be(WalletSliceState.Expired);
        }
        else
        {
            sliceDb.State.Should().Be(WalletSliceState.Available);
        }
    }

    [Fact]
    public async Task ExpireSlices_AcrossDifferentCertificates()
    {
        var daysBeforeCertificatesExpire = DateTimeOffset.UtcNow.AddDays(-60);

        var registry = _fixture.Create<string>();
        var startDate = daysBeforeCertificatesExpire.AddHours(-1);
        var certificate1 = await CreateCertificate(registry, startDate: startDate, endDate: startDate.AddHours(1));
        var certificate2 = await CreateCertificate(registry, startDate: startDate, endDate: startDate.AddHours(1));
        var wallet = await CreateWallet(registry);
        var endpoint = await CreateWalletEndpoint(wallet);
        var slice1 = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = 1,
            RegistryName = registry,
            CertificateId = certificate1.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };
        var slice2 = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = 1,
            RegistryName = registry,
            CertificateId = certificate2.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };

        await _certRepository.InsertWalletSlice(slice1);
        await _certRepository.InsertWalletSlice(slice2);

        await _certRepository.ExpireSlices(daysBeforeCertificatesExpire);

        var slice1Db = await _certRepository.GetWalletSlice(slice1.Id);
        var slice2Db = await _certRepository.GetWalletSlice(slice2.Id);

        slice1Db.State.Should().Be(WalletSliceState.Expired);
        slice2Db.State.Should().Be(WalletSliceState.Expired);
    }

    [Theory]
    [InlineData(WalletSliceState.Slicing)]
    [InlineData(WalletSliceState.Registering)]
    [InlineData(WalletSliceState.Sliced)]
    [InlineData(WalletSliceState.Claimed)]
    [InlineData(WalletSliceState.Reserved)]
    public async Task ExpireSlices_WhenPartWasOtherStatusThanClaimed_OnlyExpireAvailable(WalletSliceState state)
    {
        var daysBeforeCertificatesExpire = DateTimeOffset.UtcNow.AddDays(-60);

        var owner = _fixture.Create<string>();
        var registry = _fixture.Create<string>();
        var startDate = daysBeforeCertificatesExpire.AddHours(-1);
        var certificate = await CreateCertificate(registry, startDate: startDate, endDate: startDate.AddHours(1));
        var wallet = await CreateWallet(owner);
        var endpoint = await CreateWalletEndpoint(wallet);
        var slice = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = 1,
            RegistryName = registry,
            CertificateId = certificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };
        var otherStateSlice = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = 1,
            RegistryName = registry,
            CertificateId = certificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = state
        };
        await _certRepository.InsertWalletSlice(slice);
        await _certRepository.InsertWalletSlice(otherStateSlice);

        await _certRepository.ExpireSlices(daysBeforeCertificatesExpire);

        var sliceDb = await _certRepository.GetWalletSlice(slice.Id);
        var claimedSliceDb = await _certRepository.GetWalletSlice(otherStateSlice.Id);

        sliceDb.State.Should().Be(WalletSliceState.Expired);
        claimedSliceDb.State.Should().Be(state);
    }

    [Fact]
    public async Task GetCertificates_DoesNotGetExpiredCertificates()
    {
        var daysBeforeCertificatesExpire = DateTimeOffset.UtcNow.AddDays(-60);

        var owner = _fixture.Create<string>();
        var registry = _fixture.Create<string>();
        var startDate = daysBeforeCertificatesExpire.AddHours(-1);
        var certificate = await CreateCertificate(registry, startDate: startDate, endDate: startDate.AddHours(1));
        var wallet = await CreateWallet(owner);
        var endpoint = await CreateWalletEndpoint(wallet);
        var slice = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = 1,
            RegistryName = registry,
            CertificateId = certificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };
        await _certRepository.InsertWalletSlice(slice);

        await _certRepository.ExpireSlices(daysBeforeCertificatesExpire);

        var queryResult1 = await _certRepository.QueryCertificates(new QueryCertificatesFilterCursor
        {
            Owner = owner,
            UpdatedSince = null
        });
        queryResult1.Items.Should().BeEmpty();

        var queryResult2 = await _certRepository.QueryAvailableCertificates(new QueryCertificatesFilter
        {
            Owner = owner
        });
        queryResult2.Items.Should().BeEmpty();

        var aggregated = await _certRepository.QueryAggregatedAvailableCertificates(new QueryAggregatedCertificatesFilter
        {
            Owner = owner,
            TimeAggregate = TimeAggregate.Day,
            TimeZone = "Europe/Copenhagen"
        });
        aggregated.Items.Should().BeEmpty();

    }

    [Fact]
    public async Task GetAllOwnedCertificates()
    {
        // Arrange
        var endpointPosition = 1;
        var registry = _fixture.Create<string>();
        var certificate1 = await CreateCertificate(registry);
        var certificate2 = await CreateCertificate(registry, GranularCertificateType.Consumption);
        var certificate3 = await CreateCertificate(registry);

        var owner1 = _fixture.Create<string>();
        var wallet1 = await CreateWallet(owner1);
        var owner1Endpoint1 = await CreateWalletEndpoint(wallet1);
        var owner1Endpoint2 = await CreateWalletEndpoint(wallet1);

        var owner2 = _fixture.Create<string>();
        var wallet2 = await CreateWallet(owner2);
        var endpoint3 = await CreateWalletEndpoint(wallet2);
        //Wallet1
        var slice1 = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = owner1Endpoint1.Id,
            WalletEndpointPosition = endpointPosition,
            RegistryName = registry,
            CertificateId = certificate1.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };
        var slice2 = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = owner1Endpoint1.Id,
            WalletEndpointPosition = endpointPosition + 1,
            RegistryName = registry,
            CertificateId = certificate1.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };
        //Certficiate2
        var slice3 = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = owner1Endpoint2.Id,
            WalletEndpointPosition = endpointPosition,
            RegistryName = registry,
            CertificateId = certificate2.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };

        var sliceWithDifferentOwner = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint3.Id,
            WalletEndpointPosition = endpointPosition,
            RegistryName = registry,
            CertificateId = certificate3.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };

        await _certRepository.InsertWalletSlice(slice1);
        await _certRepository.InsertWalletSlice(slice2);
        await _certRepository.InsertWalletSlice(slice3);
        await _certRepository.InsertWalletSlice(sliceWithDifferentOwner);

        var result = await _certRepository.QueryAvailableCertificates(new QueryCertificatesFilter
        {
            Owner = owner1
        });

        result.Items.Should().HaveCount(2).And.Satisfy(
            c => c.CertificateId == certificate1.Id && c.Quantity == slice1.Quantity + slice2.Quantity,
            c => c.CertificateId == certificate2.Id && c.Quantity == slice3.Quantity
        );
    }

    [Fact]
    public async Task GetAllOwnedCertificates_WhenTypeIsConsumption()
    {
        // Arrange
        var endpointPosition = 1;
        var registry = _fixture.Create<string>();

        var owner1 = _fixture.Create<string>();
        var wallet1 = await CreateWallet(owner1);
        var owner1Endpoint1 = await CreateWalletEndpoint(wallet1);
        var owner1Endpoint2 = await CreateWalletEndpoint(wallet1);

        var owner2 = _fixture.Create<string>();
        var wallet2 = await CreateWallet(owner2);
        var owner2Endpoint1 = await CreateWalletEndpoint(wallet2);

        var prodCertificate = await CreateCertificate(registry, GranularCertificateType.Production);
        var prodSlice = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = owner1Endpoint1.Id,
            WalletEndpointPosition = endpointPosition,
            RegistryName = registry,
            CertificateId = prodCertificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };

        var consumptionCertificate = await CreateCertificate(registry, GranularCertificateType.Consumption);
        var consSlice = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = owner1Endpoint2.Id,
            WalletEndpointPosition = endpointPosition,
            RegistryName = registry,
            CertificateId = consumptionCertificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };

        var sliceWithDifferentOwner = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = owner2Endpoint1.Id,
            WalletEndpointPosition = endpointPosition,
            RegistryName = registry,
            CertificateId = prodCertificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };

        await _certRepository.InsertWalletSlice(prodSlice);
        await _certRepository.InsertWalletSlice(consSlice);
        await _certRepository.InsertWalletSlice(sliceWithDifferentOwner);

        var result = await _certRepository.QueryAvailableCertificates(new QueryCertificatesFilter
        {
            Owner = owner1,
            Type = GranularCertificateType.Consumption
        });

        result.Items.Should().HaveCount(1).And.Satisfy(
            foundCertificate => foundCertificate.CertificateId == consumptionCertificate.Id
                                && foundCertificate.Quantity == consSlice.Quantity
        );
    }

    [Fact]
    public async Task GetAllOwnedCertificates_WhenFiltering_Range()
    {
        var owner = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);
        var startDate = DateTimeOffset.Now.ToUtcTime();

        await CreateCertificatesAndSlices(wallet, 5, startDate);

        var result = await _certRepository.QueryAvailableCertificates(new QueryCertificatesFilter
        {
            Owner = owner,
            Start = startDate,
            End = startDate.AddHours(4)
        });

        result.Items.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetAllOwnedCertificates_WhenFiltering_StartDate()
    {
        var owner = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);
        var startDate = DateTimeOffset.Now.ToUtcTime();
        var numberOfCerts = 42;

        await CreateCertificatesAndSlices(wallet, numberOfCerts, startDate);

        var result = await _certRepository.QueryAvailableCertificates(new QueryCertificatesFilter
        {
            Owner = owner,
            Start = startDate.AddHours(numberOfCerts - 4)
        });

        result.Items.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetAllOwnedCertificates_WhenFiltering_EndDate()
    {
        var owner = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);
        var startDate = DateTimeOffset.Now.ToUtcTime();
        var numberOfCerts = 42;

        await CreateCertificatesAndSlices(wallet, numberOfCerts, startDate);

        var result = await _certRepository.QueryAvailableCertificates(new QueryCertificatesFilter
        {
            Owner = owner,
            End = startDate.AddHours(4)
        });

        result.Items.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetAllOwnedCertificates_WalletAttributes()
    {
        // Arrange
        var endpointPosition = 1;
        var registry = _fixture.Create<string>();
        var owner = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);
        var endpoint = await CreateWalletEndpoint(wallet);

        var certificateId = Guid.NewGuid();
        WalletAttribute assetIdWalletAttribute = new WalletAttribute
        {
            Key = "AssetId",
            Value = "571234567890123456",
            CertificateId = certificateId,
            RegistryName = registry,
            Salt = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())
        };

        var attributes = new List<CertificateAttribute>
        {
            new()
            {
                Key = "AssetId", Value = assetIdWalletAttribute.GetHashedValue(), Type = CertificateAttributeType.Hashed
            },
            new() { Key = "TechCode", Value = "T070000", Type = CertificateAttributeType.ClearText },
            new() { Key = "FuelCode", Value = "F00000000", Type = CertificateAttributeType.ClearText },
        };
        var certificate = new Certificate
        {
            Id = certificateId,
            RegistryName = registry,
            StartDate = DateTimeOffset.Now.ToUtcTime(),
            EndDate = DateTimeOffset.Now.AddDays(1).ToUtcTime(),
            GridArea = "DK1",
            CertificateType = GranularCertificateType.Production,
            Attributes = attributes,
            Withdrawn = false
        };

        var slice = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = endpointPosition,
            RegistryName = registry,
            CertificateId = certificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };

        await _certRepository.InsertCertificate(certificate);
        await _certRepository.InsertWalletAttribute(wallet.Id, assetIdWalletAttribute);
        await _certRepository.InsertWalletSlice(slice);

        // Act
        var result = await _certRepository.QueryAvailableCertificates(new QueryCertificatesFilter
        {
            Owner = owner
        });

        // Assert
        result.Items.Should().HaveCount(1).And.Satisfy(
            c => c.CertificateId == certificate.Id
                 && c.Quantity == slice.Quantity
                 && c.Attributes.Count == 3
                 && c.Attributes.SingleOrDefault(x =>
                     x.Key == assetIdWalletAttribute.Key && x.Value == assetIdWalletAttribute.Value) != null
        );
    }

    [Theory]
    [InlineData(WalletSliceState.Available, 1)]
    [InlineData(WalletSliceState.Registering, 0)]
    [InlineData(WalletSliceState.Slicing, 0)]
    [InlineData(WalletSliceState.Sliced, 0)]
    [InlineData(WalletSliceState.Claimed, 0)]
    [InlineData(WalletSliceState.Reserved, 0)]
    public async Task GetAllOwnedCertificates_AllSliceStates(WalletSliceState sliceState, int expectedCertificateCount)
    {
        // Arrange
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
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };

        // Act
        await _certRepository.InsertWalletSlice(slice);
        await _certRepository.SetWalletSliceState(slice.Id, sliceState);

        var result = await _certRepository.QueryAvailableCertificates(new QueryCertificatesFilter
        {
            Owner = owner
        });

        // Assert
        result.Items.Should().HaveCount(expectedCertificateCount);
    }

    [Fact]
    public async Task GetAvailableSlice()
    {
        var registry = _fixture.Create<string>();
        var wallet1 = await CreateWallet(_fixture.Create<string>());

        var slice = await CreateAndInsertCertificateWithSlice(registry, await CreateWalletEndpoint(wallet1), 1);

        var sliceDb = await _certRepository.GetOwnersAvailableSlices(registry, slice.CertificateId, wallet1.Owner);

        sliceDb.Should().HaveCount(1);
        sliceDb.Should().ContainEquivalentOf(slice, options => options.Excluding(x => x.UpdatedAt));
    }

    [Fact]
    public async Task GetAvailableSlice_WhenSliceStateOtherThanAvailable_ExpectNull()
    {
        var registry = _fixture.Create<string>();
        var wallet1 = await CreateWallet(_fixture.Create<string>());
        var endpointPosition = 1;
        var endpoint = await CreateWalletEndpoint(wallet1);
        var certificate = await CreateCertificate(registry);
        var slice1 = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = endpointPosition,
            RegistryName = registry,
            CertificateId = certificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Sliced
        };
        var slice2 = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = endpointPosition,
            RegistryName = registry,
            CertificateId = certificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Slicing
        };

        await _certRepository.InsertWalletSlice(slice1);
        await _certRepository.InsertWalletSlice(slice2);

        var sliceDb = await _certRepository.GetOwnersAvailableSlices(registry, certificate.Id, wallet1.Owner);

        sliceDb.Should().BeEmpty();
    }

    [Fact]
    public async Task SetSliceState()
    {
        var registry = _fixture.Create<string>();
        var wallet1 = await CreateWallet(_fixture.Create<string>());
        var endpointPosition = 1;
        var endpoint = await CreateWalletEndpoint(wallet1);
        var certificate = await CreateCertificate(registry);
        var slice = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = endpointPosition,
            RegistryName = registry,
            CertificateId = certificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };

        await _certRepository.InsertWalletSlice(slice);

        await _certRepository.SetWalletSliceState(slice.Id, WalletSliceState.Slicing);

        var sliceDb = await _certRepository.GetWalletSlice(slice.Id);

        sliceDb.State.Should().Be(WalletSliceState.Slicing);
    }

    [Fact]
    public async Task ReserveSlice()
    {
        // Arrange
        var registry = _fixture.Create<string>();
        var owner = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);
        var slice = await CreateAndInsertCertificateWithSlice(registry, await CreateWalletEndpoint(wallet), 1,
            quantity: 150);

        // Act
        var reservedSlices = await _certRepository.ReserveQuantity(owner, slice.RegistryName, slice.CertificateId, 100);

        // Assert
        reservedSlices.Should().ContainEquivalentOf(slice, options => options.Excluding(x => x.UpdatedAt));
    }

    [Fact]
    public async Task ReserveSlice_NoSlices_ThrowsException()
    {
        // Arrange
        var registry = _fixture.Create<string>();
        var certificate = await CreateCertificate(registry);
        var owner = _fixture.Create<string>();

        // Act
        var act = () => _certRepository.ReserveQuantity(owner, certificate.RegistryName, certificate.Id, 200);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Owner has less to reserve than available");
    }

    [Fact]
    public async Task ReserveSlice_OnlyWithdrawnSlices_ThrowException()
    {
        var registry = _fixture.Create<string>();
        var owner = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);
        var slice = await CreateAndInsertCertificateWithSlice(registry, await CreateWalletEndpoint(wallet), 1,
            quantity: 200);

        await _certRepository.WithdrawCertificate(registry, slice.CertificateId);

        var act = () => _certRepository.ReserveQuantity(owner, slice.RegistryName, slice.CertificateId, 200);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Owner has less to reserve than available");
    }

    [Fact]
    public async Task ReserveSlice_LessThan_ThrowsException()
    {
        // Arrange
        var registry = _fixture.Create<string>();
        var owner = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);
        var endpoint = await CreateWalletEndpoint(wallet);
        var slice = await CreateAndInsertCertificateWithSlice(registry, await CreateWalletEndpoint(wallet), 1,
            quantity: 150);

        // Act
        var act = () => _certRepository.ReserveQuantity(owner, slice.RegistryName, slice.CertificateId, 200);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Owner has less to reserve than available");
    }

    [Fact]
    public async Task ReserveSlice_ToBe_ThrowsException()
    {
        // Arrange
        var registry = _fixture.Create<string>();
        var owner = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);

        var slice = await CreateAndInsertCertificateWithSlice(registry, await CreateWalletEndpoint(wallet), 1,
            quantity: 150);
        await _certRepository.InsertWalletSlice(slice with
        {
            Id = Guid.NewGuid(),
            WalletEndpointPosition = 2,
            Quantity = 75,
            State = WalletSliceState.Registering,
        });

        // Act
        var act = () => _certRepository.ReserveQuantity(owner, slice.RegistryName, slice.CertificateId, 200);

        // Assert
        await act.Should().ThrowAsync<QuantityNotYetAvailableToReserveException>()
            .WithMessage("Owner has enough quantity, but it is not yet available to reserve");
    }

    [Fact]
    public async Task InsertAndGetWalletAttributes()
    {
        // Arrange
        var registry = _fixture.Create<string>();
        var owner = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);

        var certificateId = Guid.NewGuid();
        WalletAttribute assetIdWalletAttribute = new WalletAttribute
        {
            Key = "AssetId",
            Value = "571234567890123456",
            CertificateId = certificateId,
            RegistryName = registry,
            Salt = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())
        };

        var attributes = new List<CertificateAttribute>
        {
            new()
            {
                Key = assetIdWalletAttribute.Key, Value = assetIdWalletAttribute.GetHashedValue(),
                Type = CertificateAttributeType.Hashed
            },
            new() { Key = "TechCode", Value = "T070000", Type = CertificateAttributeType.ClearText },
            new() { Key = "FuelCode", Value = "F00000000", Type = CertificateAttributeType.ClearText },
        };
        var certificate = new Certificate
        {
            Id = certificateId,
            RegistryName = registry,
            StartDate = DateTimeOffset.Now.ToUtcTime(),
            EndDate = DateTimeOffset.Now.AddDays(1).ToUtcTime(),
            GridArea = "DK1",
            CertificateType = GranularCertificateType.Production,
            Attributes = attributes,
            Withdrawn = false
        };
        await _certRepository.InsertCertificate(certificate);

        // Act
        await _certRepository.InsertWalletAttribute(wallet.Id, assetIdWalletAttribute);
        var walletAttributeFromRepo = await _certRepository.GetWalletAttributes(wallet.Id, certificate.Id,
            certificate.RegistryName, new string[] { assetIdWalletAttribute.Key });

        // Assert
        var foundAttribute = walletAttributeFromRepo.Should().ContainSingle().Which;
        foundAttribute.Value.Should().Be(assetIdWalletAttribute.Value);
    }

    [Theory]
    [InlineData("2020-06-08T12:00:00Z", "2020-06-10T12:00:00Z", 10, 0, 10, 48)]
    [InlineData("2020-06-08T12:00:00Z", "2020-06-10T12:00:00Z", 10, 20, 10, 48)]
    [InlineData("2020-06-08T12:00:00Z", "2020-06-10T12:00:00Z", 10, 40, 8, 48)]
    public async Task QueryCertificates_Pagination(string from, string to, int take, int skip, int numberOfResults,
        int total)
    {
        // Arrange
        var owner = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);
        var startDate = new DateTimeOffset(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateCertificatesAndSlices(wallet, 31 * 24, startDate);

        // Act
        var result = await _certRepository.QueryAvailableCertificates(new QueryCertificatesFilter
        {
            Owner = owner,
            Start = DateTimeOffset.Parse(from),
            End = DateTimeOffset.Parse(to),
            Limit = take,
            Skip = skip,
            SortBy = "End",
            Sort = "ASC"
        });

        // Assert
        result.Items.Should().HaveCount(numberOfResults);
        result.Items.Should().BeInAscendingOrder(x => x.EndDate);
        result.Offset.Should().Be(skip);
        result.Limit.Should().Be(take);
        result.Count.Should().Be(numberOfResults);
        result.TotalCount.Should().Be(total);
    }

    [Fact]
    public async Task QuerySingleCertificate()
    {
        var owner = _fixture.Create<string>();
        var registry = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);

        var startDate = DateTimeOffset.UtcNow.AddHours(-1);
        var cert = await CreateCertificate(registry, GranularCertificateType.Consumption, startDate, endDate: startDate.AddHours(1));
        var slice = await CreateAndInsertCertificateWithSlice(registry, await CreateWalletEndpoint(wallet), 1, 100, cert);


        var result = await _certRepository.QueryCertificate(owner, registry, cert.Id);

        result!.RegistryName.Should().Be(registry);
        result.CertificateId.Should().Be(cert.Id);
        result.Quantity.Should().Be((uint)slice.Quantity);
        result.Attributes.Should().BeEquivalentTo(cert.Attributes);
    }

    [Fact]
    public async Task QueryCertificates_UpdatedSinceNull()
    {
        // Arrange
        var owner = _fixture.Create<string>();

        var wallet = await CreateWallet(owner);
        var startDate = new DateTimeOffset(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateCertificatesAndSlices(wallet, 1, startDate);

        // Act
        var result = await _certRepository.QueryCertificates(new QueryCertificatesFilterCursor
        {
            Owner = owner,
            Start = startDate,
            End = DateTimeOffset.Parse("2020-06-10T11:00:00Z"),
            Limit = 10,
            UpdatedSince = DateTimeOffset.UtcNow.AddHours(-1)
        });

        // Assert

        result.Items.Should().HaveCount(1);
        result.Limit.Should().Be(10);
        result.Count.Should().Be(1);
        result.TotalCount.Should().Be(1);
        result.Items.Should().BeInAscendingOrder(x => x.UpdatedAt);
    }

    [Fact]
    public async Task QueryCertificates_cursor()
    {
        // Arrange
        var owner = _fixture.Create<string>();

        var wallet = await CreateWallet(owner);
        var startDate = new DateTimeOffset(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateCertificatesAndSlices(wallet, 50, startDate, 10);

        // Act
        var result = await _certRepository.QueryCertificates(new QueryCertificatesFilterCursor
        {
            Owner = owner,
            Start = startDate,
            End = DateTimeOffset.Parse("2020-06-10T11:00:00Z"),
            Limit = 10,
            UpdatedSince = DateTimeOffset.UtcNow.AddHours(-1)
        });

        // Assert

        result.Items.Should().HaveCount(10);
        result.Limit.Should().Be(10);
        result.Count.Should().Be(10);
        result.TotalCount.Should().Be(50);
        result.Items.Should().BeInAscendingOrder(x => x.UpdatedAt);

        var cursor = result.Items.Last().UpdatedAt;
        var result2 = await _certRepository.QueryCertificates(new QueryCertificatesFilterCursor
        {
            Owner = owner,
            Start = startDate,
            End = DateTimeOffset.Parse("2020-06-10T11:00:00Z"),
            Limit = 10,
            UpdatedSince = cursor
        });

        // test result2 is the next page
        result2.Items.First().UpdatedAt.Should().BeAfter(cursor);
    }

    [Theory]
    [InlineData("2020-06-08T12:00:00Z", "2020-06-12T12:00:00Z", TimeAggregate.Total, "Europe/Copenhagen", 2, 0, 1, 1)]
    [InlineData("2020-06-08T12:00:00Z", "2020-06-12T12:00:00Z", TimeAggregate.Day, "Europe/Copenhagen", 2, 2, 2, 5)]
    [InlineData("2020-06-08T00:00:00Z", "2020-06-12T00:00:00Z", TimeAggregate.Day, "Europe/Copenhagen", 2, 4, 1, 5)]
    [InlineData("2020-06-01T00:00:00Z", "2020-06-03T12:00:00Z", TimeAggregate.Day, "Europe/Copenhagen", 2, 0, 2, 3)]
    [InlineData("2020-06-01T00:00:00Z", "2020-06-03T12:00:00Z", TimeAggregate.Day, "America/Toronto", 2, 0, 2, 4)]
    public async Task QueryAggregatedCertificates_Pagination(string from, string to, TimeAggregate aggregate,
        string timeZone, int take, int skip, int numberOfResults, int total)
    {
        // Arrange
        var owner = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);
        var startDate = new DateTimeOffset(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateCertificatesAndSlices(wallet, 31 * 24, startDate);

        // Act
        var result = await _certRepository.QueryAggregatedAvailableCertificates(new QueryAggregatedCertificatesFilter
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

    private async Task CreateCertificatesAndSlices(Wallet wallet, int numberOfCertificates, DateTimeOffset startDate, int delay = 0)
    {
        var registry = _fixture.Create<string>();
        var endpoint = await CreateWalletEndpoint(wallet);

        var position = 1;
        for (int i = 0; i < numberOfCertificates; i++)
        {
            var cert = await CreateCertificate(registry, GranularCertificateType.Consumption, startDate.AddHours(i), endDate: startDate.AddHours(i + 1));
            var slice = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = endpoint.Id,
                WalletEndpointPosition = position++,
                RegistryName = registry,
                CertificateId = cert.Id,
                Quantity = _fixture.Create<int>(),
                RandomR = _fixture.Create<byte[]>(),
                State = WalletSliceState.Available
            };

            await _certRepository.InsertWalletSlice(slice);
            await Task.Delay(delay);
        }
    }

    private async Task<WalletSlice> CreateAndInsertCertificateWithSlice(
        string registry,
        WalletEndpoint endpoint,
        int endpointPosition,
        int? quantity = null, Certificate? cert = null)
    {
        quantity = quantity ?? _fixture.Create<int>();

        var certificate = cert ?? await CreateCertificate(registry);
        var slice = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = endpointPosition,
            RegistryName = registry,
            CertificateId = certificate.Id,
            Quantity = quantity.Value,
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };

        await _certRepository.InsertWalletSlice(slice);

        return slice;
    }
}
