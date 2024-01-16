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

namespace ProjectOrigin.WalletSystem.IntegrationTests.Repositories;

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
            new(){ Key="AssetId", Value="571234567890123456", Type=CertificateAttributeType.ClearText},
            new(){ Key="TechCode", Value="T070000", Type=CertificateAttributeType.ClearText},
            new(){ Key="FuelCode", Value="F00000000", Type=CertificateAttributeType.ClearText},
        };
        var certificate = new Certificate
        {
            Id = Guid.NewGuid(),
            RegistryName = registry,
            StartDate = DateTimeOffset.Now.ToUtcTime(),
            EndDate = DateTimeOffset.Now.AddDays(1).ToUtcTime(),
            GridArea = "DK1",
            CertificateType = GranularCertificateType.Production,
            Attributes = attributes
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
        var insertedSlice = await _connection.QueryFirstOrDefaultAsync<WalletSlice>("SELECT * FROM wallet_slices WHERE id = @id", new { slice.Id });
        insertedSlice.Should().BeEquivalentTo(slice);
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
        var endpoint1 = await CreateWalletEndpoint(wallet1);
        var endpoint2 = await CreateWalletEndpoint(wallet1);
        var owner2 = _fixture.Create<string>();
        var wallet2 = await CreateWallet(owner2);
        var endpoint3 = await CreateWalletEndpoint(wallet2);
        //Wallet1
        var slice1 = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint1.Id,
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
            WalletEndpointId = endpoint1.Id,
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
            WalletEndpointId = endpoint2.Id,
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

        var result = await _certRepository.QueryAvailableCertificates(new CertificatesFilter
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

        var result = await _certRepository.QueryAvailableCertificates(new CertificatesFilter
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

        var result = await _certRepository.QueryAvailableCertificates(new CertificatesFilter
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

        var result = await _certRepository.QueryAvailableCertificates(new CertificatesFilter
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

        var result = await _certRepository.QueryAvailableCertificates(new CertificatesFilter
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
            new(){ Key="AssetId", Value=assetIdWalletAttribute.GetHashedValue(), Type=CertificateAttributeType.Hashed},
            new(){ Key="TechCode", Value="T070000", Type=CertificateAttributeType.ClearText},
            new(){ Key="FuelCode", Value="F00000000", Type=CertificateAttributeType.ClearText},
        };
        var certificate = new Certificate
        {
            Id = certificateId,
            RegistryName = registry,
            StartDate = DateTimeOffset.Now.ToUtcTime(),
            EndDate = DateTimeOffset.Now.AddDays(1).ToUtcTime(),
            GridArea = "DK1",
            CertificateType = GranularCertificateType.Production,
            Attributes = attributes
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
        var result = await _certRepository.QueryAvailableCertificates(new CertificatesFilter
        {
            Owner = owner
        });

        // Assert
        result.Items.Should().HaveCount(1).And.Satisfy(
            c => c.CertificateId == certificate.Id
                 && c.Quantity == slice.Quantity
                 && c.Attributes.Count == 3
                 && c.Attributes.SingleOrDefault(x => x.Key == assetIdWalletAttribute.Key && x.Value == assetIdWalletAttribute.Value) != null
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

        var result = await _certRepository.QueryAvailableCertificates(new CertificatesFilter
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

        var sliceDb = await _certRepository.GetOwnersAvailableSlices(registry, slice.CertificateId, wallet1.Owner);

        sliceDb.Should().HaveCount(1);
        sliceDb.Should().ContainEquivalentOf(slice);
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
        await _certRepository.InsertWalletSlice(slice);

        // Act
        var reservedSlices = await _certRepository.ReserveQuantity(owner, certificate.RegistryName, certificate.Id, 100);

        // Assert
        reservedSlices.Should().ContainEquivalentOf(slice);
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
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Owner has no available slices to reserve");
    }

    [Fact]
    public async Task ReserveSlice_LessThan_ThrowsException()
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
            Quantity = 150,
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };
        await _certRepository.InsertWalletSlice(slice);

        // Act
        var act = () => _certRepository.ReserveQuantity(owner, certificate.RegistryName, certificate.Id, 200);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Owner has less to reserve than available");
    }

    [Fact]
    public async Task ReserveSlice_ToBe_ThrowsException()
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
            Quantity = 150,
            RandomR = _fixture.Create<byte[]>(),
            State = WalletSliceState.Available
        };
        await _certRepository.InsertWalletSlice(slice);
        await _certRepository.InsertWalletSlice(slice with
        {
            Id = Guid.NewGuid(),
            WalletEndpointPosition = 2,
            Quantity = 75,
            State = WalletSliceState.Registering,
        });

        // Act
        var act = () => _certRepository.ReserveQuantity(owner, certificate.RegistryName, certificate.Id, 200);

        // Assert
        await act.Should().ThrowAsync<TransientException>().WithMessage("Owner has enough quantity, but it is not yet available to reserve");
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
            new(){ Key=assetIdWalletAttribute.Key, Value=assetIdWalletAttribute.GetHashedValue(), Type=CertificateAttributeType.Hashed},
            new(){ Key="TechCode", Value="T070000", Type=CertificateAttributeType.ClearText},
            new(){ Key="FuelCode", Value="F00000000", Type=CertificateAttributeType.ClearText},
        };
        var certificate = new Certificate
        {
            Id = certificateId,
            RegistryName = registry,
            StartDate = DateTimeOffset.Now.ToUtcTime(),
            EndDate = DateTimeOffset.Now.AddDays(1).ToUtcTime(),
            GridArea = "DK1",
            CertificateType = GranularCertificateType.Production,
            Attributes = attributes
        };
        await _certRepository.InsertCertificate(certificate);

        // Act
        await _certRepository.InsertWalletAttribute(wallet.Id, assetIdWalletAttribute);
        var walletAttributeFromRepo = await _certRepository.GetWalletAttributes(wallet.Id, certificate.Id, certificate.RegistryName, new string[] { assetIdWalletAttribute.Key });

        // Assert
        var foundAttribute = walletAttributeFromRepo.Should().ContainSingle().Which;
        foundAttribute.Value.Should().Be(assetIdWalletAttribute.Value);
    }

    [Theory]
    [InlineData("2020-06-08T12:00:00", "2020-06-10T12:00:00", 10, 0, 10, 48)]
    [InlineData("2020-06-08T12:00:00", "2020-06-10T12:00:00", 10, 20, 10, 48)]
    [InlineData("2020-06-08T12:00:00", "2020-06-10T12:00:00", 10, 40, 8, 48)]
    public async Task QueryCertificates_Pagination(string from, string to, int take, int skip, int numberOfResults, int total)
    {
        // Arrange
        var owner = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);
        var startDate = new DateTimeOffset(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateCertificatesAndSlices(wallet, 31 * 24, startDate);

        // Act
        var result = await _certRepository.QueryAvailableCertificates(new CertificatesFilter
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

    [Theory]
    [InlineData("2020-06-08T12:00:00", "2020-06-12T12:00:00", TimeAggregate.Total, "Europe/Copenhagen", 2, 0, 1, 1)]
    [InlineData("2020-06-08T12:00:00", "2020-06-12T12:00:00", TimeAggregate.Day, "Europe/Copenhagen", 2, 2, 2, 5)]
    [InlineData("2020-06-08T00:00:00", "2020-06-12T00:00:00", TimeAggregate.Day, "Europe/Copenhagen", 2, 4, 1, 5)]
    [InlineData("2020-06-01T00:00:00", "2020-06-03T12:00:00", TimeAggregate.Day, "Europe/Copenhagen", 2, 0, 2, 3)]
    [InlineData("2020-06-01T00:00:00", "2020-06-03T12:00:00", TimeAggregate.Day, "America/Toronto", 2, 0, 2, 4)]
    public async Task QueryAggregatedCertificates_Pagination(string from, string to, TimeAggregate aggregate, string timeZone, int take, int skip, int numberOfResults, int total)
    {
        // Arrange
        var owner = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);
        var startDate = new DateTimeOffset(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateCertificatesAndSlices(wallet, 31 * 24, startDate);

        // Act
        var result = await _certRepository.QueryAggregatedAvailableCertificates(new CertificatesFilter
        {
            Owner = owner,
            Start = DateTimeOffset.Parse(from),
            End = DateTimeOffset.Parse(to),
            Limit = take,
            Skip = skip,
        }, aggregate, timeZone);

        //assert
        result.Items.Should().HaveCount(numberOfResults);
        result.Offset.Should().Be(skip);
        result.Limit.Should().Be(take);
        result.Count.Should().Be(numberOfResults);
        result.TotalCount.Should().Be(total);
    }

    private async Task CreateCertificatesAndSlices(Wallet wallet, int numberOfCertificates, DateTimeOffset startDate)
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
        }
    }
}
