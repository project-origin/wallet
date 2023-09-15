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

namespace ProjectOrigin.WalletSystem.IntegrationTests.Repositories;

public class CertificateRepositoryTests : AbstractRepositoryTests
{
    private readonly CertificateRepository _repository;

    public CertificateRepositoryTests(PostgresDatabaseFixture dbFixture) : base(dbFixture)
    {
        _repository = new CertificateRepository(_connection);
    }

    [Fact]
    public async Task InsertCertificate_InsertsCertificate()
    {
        // Arrange
        var registry = _fixture.Create<string>();
        var attributes = new List<CertificateAttribute>
        {
            new (){ Key="AssetId", Value= "571234567890123456"},
            new (){ Key="TechCode", Value= "T070000"},
            new (){ Key="FuelCode", Value= "F00000000"}
        };
        var certificate = new Certificate
        {
            Id = Guid.NewGuid(),
            Registry = registry,
            StartDate = DateTimeOffset.Now.ToUtcTime(),
            EndDate = DateTimeOffset.Now.AddDays(1).ToUtcTime(),
            GridArea = "DK1",
            CertificateType = GranularCertificateType.Production,
            Attributes = attributes
        };

        // Act
        await _repository.InsertCertificate(certificate);

        // Assert
        var insertedCertificate = await _repository.GetCertificate(certificate.Registry, certificate.Id);
        insertedCertificate.Should().BeEquivalentTo(certificate);
    }

    [Fact]
    public async Task GetCertificate_ReturnsCertificate()
    {
        // Arrange
        var registry = _fixture.Create<string>();
        var certificate = await CreateCertificate(registry);

        // Act
        var result = await _repository.GetCertificate(registry, certificate.Id);

        // Assert
        result.Should().BeEquivalentTo(certificate);
    }

    [Fact]
    public async Task CreateSlice_InsertsSlice()
    {
        // Arrange
        var depositEndpointPosition = 1;
        var registry = _fixture.Create<string>();
        var certificate = await CreateCertificate(registry);
        var wallet = await CreateWallet(registry);
        var depositEndpoint = await CreateDepositEndpoint(wallet);
        var slice = new Slice
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = depositEndpoint.Id,
            DepositEndpointPosition = depositEndpointPosition,
            Registry = registry,
            CertificateId = certificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            SliceState = SliceState.Available
        };

        // Act
        await _repository.InsertSlice(slice);

        // Assert
        var insertedSlice = await _connection.QueryFirstOrDefaultAsync<Slice>("SELECT s.*, r.Name as Registry FROM Slices s INNER JOIN Registries r ON r.id = s.registryId  WHERE s.Id = @id", new { slice.Id });
        insertedSlice.Should().BeEquivalentTo(slice);
    }

    [Fact]
    public async Task GetAllOwnedCertificates()
    {
        // Arrange
        var deposintEndpointPosition = 1;
        var registry = _fixture.Create<string>();
        var certificate1 = await CreateCertificate(registry);
        var certificate2 = await CreateCertificate(registry, GranularCertificateType.Consumption);
        var certificate3 = await CreateCertificate(registry);
        var owner1 = _fixture.Create<string>();
        var wallet1 = await CreateWallet(owner1);
        var depositEndpoint1 = await CreateDepositEndpoint(wallet1);
        var depositEndpoint2 = await CreateDepositEndpoint(wallet1);
        var owner2 = _fixture.Create<string>();
        var wallet2 = await CreateWallet(owner2);
        var depositEndpoint3 = await CreateDepositEndpoint(wallet2);
        //Wallet1
        var slice1 = new Slice
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = depositEndpoint1.Id,
            DepositEndpointPosition = deposintEndpointPosition,
            Registry = registry,
            CertificateId = certificate1.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            SliceState = SliceState.Available
        };
        var slice2 = new Slice
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = depositEndpoint1.Id,
            DepositEndpointPosition = deposintEndpointPosition + 1,
            Registry = registry,
            CertificateId = certificate1.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            SliceState = SliceState.Available
        };
        //Certficiate2
        var slice3 = new Slice
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = depositEndpoint2.Id,
            DepositEndpointPosition = deposintEndpointPosition,
            Registry = registry,
            CertificateId = certificate2.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            SliceState = SliceState.Available
        };

        var sliceWithDifferentOwner = new Slice
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = depositEndpoint3.Id,
            DepositEndpointPosition = deposintEndpointPosition,
            Registry = registry,
            CertificateId = certificate3.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            SliceState = SliceState.Available
        };

        await _repository.InsertSlice(slice1);
        await _repository.InsertSlice(slice2);
        await _repository.InsertSlice(slice3);
        await _repository.InsertSlice(sliceWithDifferentOwner);

        var certificates = await _repository.GetAllOwnedCertificates(owner1);

        certificates.Should().HaveCount(2).And.Satisfy(
            c => c.Id == certificate1.Id && c.Slices.Sum(x => x.Quantity) == slice1.Quantity + slice2.Quantity,
            c => c.Id == certificate2.Id && c.Slices.Sum(x => x.Quantity) == slice3.Quantity
        );
    }

    [Theory]
    [InlineData(SliceState.Available, 1)]
    [InlineData(SliceState.Registering, 0)]
    [InlineData(SliceState.Slicing, 0)]
    [InlineData(SliceState.Sliced, 0)]
    [InlineData(SliceState.Transferred, 0)]
    public async Task GetAllOwnedCertificates_AllSliceStates(SliceState sliceState, int expectedCertificateCount)
    {
        // Arrange
        var registry = _fixture.Create<string>();
        var certificate = await CreateCertificate(registry);
        var owner = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);
        var depositEndpoint = await CreateDepositEndpoint(wallet);
        var slice = new Slice
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = depositEndpoint.Id,
            DepositEndpointPosition = 1,
            Registry = registry,
            CertificateId = certificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            SliceState = SliceState.Available
        };

        // Act
        await _repository.InsertSlice(slice);
        await _repository.SetSliceState(slice.Id, sliceState);

        var certificates = await _repository.GetAllOwnedCertificates(owner);

        // Assert
        certificates.Should().HaveCount(expectedCertificateCount);
    }

    [Fact]
    public async Task GetAvailableSlice()
    {
        var registry = _fixture.Create<string>();
        var wallet1 = await CreateWallet(_fixture.Create<string>());
        var depositEndpointPosition = 1;
        var depositEndpoint = await CreateDepositEndpoint(wallet1);
        var certificate = await CreateCertificate(registry);
        var slice = new Slice
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = depositEndpoint.Id,
            DepositEndpointPosition = depositEndpointPosition,
            Registry = registry,
            CertificateId = certificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            SliceState = SliceState.Available
        };

        await _repository.InsertSlice(slice);

        var sliceDb = await _repository.GetOwnerAvailableSlices(registry, slice.CertificateId, wallet1.Owner);

        sliceDb.Should().HaveCount(1);
        sliceDb.Should().ContainEquivalentOf(slice);
    }

    [Fact]
    public async Task GetAvailableSlice_WhenSliceStateOtherThanAvailable_ExpectNull()
    {
        var registry = _fixture.Create<string>();
        var wallet1 = await CreateWallet(_fixture.Create<string>());
        var depositEndpointPosition = 1;
        var depositEndpoint = await CreateDepositEndpoint(wallet1);
        var certificate = await CreateCertificate(registry);
        var slice1 = new Slice
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = depositEndpoint.Id,
            DepositEndpointPosition = depositEndpointPosition,
            Registry = registry,
            CertificateId = certificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            SliceState = SliceState.Sliced
        };
        var slice2 = new Slice
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = depositEndpoint.Id,
            DepositEndpointPosition = depositEndpointPosition,
            Registry = registry,
            CertificateId = certificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            SliceState = SliceState.Slicing
        };

        await _repository.InsertSlice(slice1);
        await _repository.InsertSlice(slice2);

        var sliceDb = await _repository.GetOwnerAvailableSlices(registry, certificate.Id, wallet1.Owner);

        sliceDb.Should().BeEmpty();
    }

    [Fact]
    public async Task SetSliceState()
    {
        var registry = _fixture.Create<string>();
        var wallet1 = await CreateWallet(_fixture.Create<string>());
        var depositEndpointPosition = 1;
        var depositEndpoint = await CreateDepositEndpoint(wallet1);
        var certificate = await CreateCertificate(registry);
        var slice = new Slice
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = depositEndpoint.Id,
            DepositEndpointPosition = depositEndpointPosition,
            Registry = registry,
            CertificateId = certificate.Id,
            Quantity = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            SliceState = SliceState.Available
        };

        await _repository.InsertSlice(slice);

        await _repository.SetSliceState(slice.Id, SliceState.Slicing);

        var sliceDb = await _repository.GetSlice(slice.Id);

        sliceDb.SliceState.Should().Be(SliceState.Slicing);
    }
}
