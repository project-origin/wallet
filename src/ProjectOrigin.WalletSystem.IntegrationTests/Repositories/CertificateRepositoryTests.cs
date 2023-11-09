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
using ProjectOrigin.WalletSystem.Server.Services.REST.v1;
using Claim = ProjectOrigin.WalletSystem.Server.Models.Claim;

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
        await _repository.InsertCertificate(certificate);

        // Assert
        var insertedCertificate = await _repository.GetCertificate(certificate.RegistryName, certificate.Id);
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
        await _repository.InsertWalletSlice(slice);

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

        await _repository.InsertWalletSlice(slice1);
        await _repository.InsertWalletSlice(slice2);
        await _repository.InsertWalletSlice(slice3);
        await _repository.InsertWalletSlice(sliceWithDifferentOwner);

        var certificates = await _repository.GetAllOwnedCertificates(owner1, new CertificatesFilter(SliceState.Available));

        certificates.Should().HaveCount(2).And.Satisfy(
            c => c.Id == certificate1.Id && c.Slices.Sum(x => x.Quantity) == slice1.Quantity + slice2.Quantity,
            c => c.Id == certificate2.Id && c.Slices.Sum(x => x.Quantity) == slice3.Quantity
        );
    }

    [Fact]
    public async Task GetAllOwnedCertificates_WhenGettingTotalCertificates()
    {
        await TruncateCertificateAndRelationsTables();
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
            State = WalletSliceState.Claimed
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
            State = WalletSliceState.Claimed
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

        await _repository.InsertWalletSlice(slice1);
        await _repository.InsertWalletSlice(slice2);
        await _repository.InsertWalletSlice(slice3);
        await _repository.InsertWalletSlice(sliceWithDifferentOwner);

        var certificates = await _repository.GetAllOwnedCertificates(owner1, new CertificatesFilter(SliceState.Total));

        certificates.Should().HaveCount(2).And.Satisfy(
            c => c.Id == certificate1.Id && c.Slices.Sum(x => x.Quantity) == slice1.Quantity + slice2.Quantity,
            c => c.Id == certificate2.Id && c.Slices.Sum(x => x.Quantity) == slice3.Quantity
        );
    }

    [Fact]
    public async Task GetAllOwnedCertificates_WhenTypeIsConsumption()
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

        await _repository.InsertWalletSlice(slice1);
        await _repository.InsertWalletSlice(slice2);
        await _repository.InsertWalletSlice(slice3);
        await _repository.InsertWalletSlice(sliceWithDifferentOwner);

        var certificates = await _repository.GetAllOwnedCertificates(owner1, new CertificatesFilter(SliceState.Available) { Type = GranularCertificateType.Consumption });

        certificates.Should().HaveCount(1).And.Satisfy(
            c => c.Id == certificate2.Id && c.Slices.Sum(x => x.Quantity) == slice3.Quantity
        );
    }

    [Fact]
    public async Task GetAllOwnedCertificates_WhenFiltering_Range()
    {
        var owner = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);
        var startDate = DateTimeOffset.Now.ToUtcTime();

        await CreateCertificatesAndSlices(wallet, 5, startDate);

        var certificates = await _repository.GetAllOwnedCertificates(owner, new CertificatesFilter(SliceState.Available)
        {
            Start = startDate,
            End = startDate.AddHours(4)
        });

        certificates.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetAllOwnedCertificates_WhenFiltering_StartDate()
    {
        var owner = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);
        var startDate = DateTimeOffset.Now.ToUtcTime();
        var numberOfCerts = 42;

        await CreateCertificatesAndSlices(wallet, numberOfCerts, startDate);

        var certificates = await _repository.GetAllOwnedCertificates(owner, new CertificatesFilter(SliceState.Available)
        {
            Start = startDate.AddHours(numberOfCerts - 4)
        });

        certificates.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetAllOwnedCertificates_WhenFiltering_EndDate()
    {
        var owner = _fixture.Create<string>();
        var wallet = await CreateWallet(owner);
        var startDate = DateTimeOffset.Now.ToUtcTime();
        var numberOfCerts = 42;

        await CreateCertificatesAndSlices(wallet, numberOfCerts, startDate);

        var certificates = await _repository.GetAllOwnedCertificates(owner, new CertificatesFilter(SliceState.Available)
        {
            End = startDate.AddHours(4)
        });

        certificates.Should().HaveCount(4);
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

        await _repository.InsertCertificate(certificate);
        await _repository.InsertWalletAttribute(wallet.Id, assetIdWalletAttribute);
        await _repository.InsertWalletSlice(slice);

        // Act
        var certificates = await _repository.GetAllOwnedCertificates(owner, new CertificatesFilter(SliceState.Available));

        // Assert
        certificates.Should().HaveCount(1).And.Satisfy(
            c => c.Id == certificate.Id
                 && c.Slices.Sum(x => x.Quantity) == slice.Quantity
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
        await _repository.InsertWalletSlice(slice);
        await _repository.SetWalletSliceState(slice.Id, sliceState);

        var certificates = await _repository.GetAllOwnedCertificates(owner, new CertificatesFilter(SliceState.Available));

        // Assert
        certificates.Should().HaveCount(expectedCertificateCount);
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

        await _repository.InsertWalletSlice(slice);

        var sliceDb = await _repository.GetOwnersAvailableSlices(registry, slice.CertificateId, wallet1.Owner);

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

        await _repository.InsertWalletSlice(slice1);
        await _repository.InsertWalletSlice(slice2);

        var sliceDb = await _repository.GetOwnersAvailableSlices(registry, certificate.Id, wallet1.Owner);

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

        await _repository.InsertWalletSlice(slice);

        await _repository.SetWalletSliceState(slice.Id, WalletSliceState.Slicing);

        var sliceDb = await _repository.GetWalletSlice(slice.Id);

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
        await _repository.InsertWalletSlice(slice);

        // Act
        var reservedSlices = await _repository.ReserveQuantity(owner, certificate.RegistryName, certificate.Id, 100);

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
        var act = () => _repository.ReserveQuantity(owner, certificate.RegistryName, certificate.Id, 200);

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
        await _repository.InsertWalletSlice(slice);

        // Act
        var act = () => _repository.ReserveQuantity(owner, certificate.RegistryName, certificate.Id, 200);

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
        await _repository.InsertWalletSlice(slice);
        await _repository.InsertWalletSlice(slice with
        {
            Id = Guid.NewGuid(),
            WalletEndpointPosition = 2,
            Quantity = 75,
            State = WalletSliceState.Registering,
        });

        // Act
        var act = () => _repository.ReserveQuantity(owner, certificate.RegistryName, certificate.Id, 200);

        // Assert
        await act.Should().ThrowAsync<TransientException>().WithMessage("Owner has enough quantity, but it is not yet available to reserve");
    }

    [Fact]
    public async Task Claims_InsertSetState_GetResult()
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
        await _repository.InsertWalletSlice(slice);
        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            ConsumptionSliceId = slice.Id,
            ProductionSliceId = slice.Id,
            State = ClaimState.Created
        };

        // Act
        await _repository.InsertClaim(claim);
        await _repository.SetClaimState(claim.Id, ClaimState.Claimed);
        var insertedClaim = await _repository.GetClaim(claim.Id);

        // Assert
        insertedClaim.Should().BeEquivalentTo(claim with { State = ClaimState.Claimed });
    }

    [Fact]
    public async Task Claims_Query_Empty()
    {
        // Arrange
        var owner = _fixture.Create<string>();

        // Act
        var claims = await _repository.GetClaims(owner, new ClaimFilter());

        // Assert
        claims.Should().NotBeNull();
        claims.Should().BeEmpty();
    }


    [Fact]
    public async Task Claims_Query_Success()
    {
        // Arrange
        var owner = _fixture.Create<string>();
        var startDate = new DateTimeOffset(2023, 7, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateClaimsAndCerts(owner, 48, startDate);

        // Act
        var claims = await _repository.GetClaims(owner, new ClaimFilter());

        // Assert
        claims.Should().NotBeNull();
        claims.Should().HaveCount(48);
        claims.Sum(x => x.Quantity).Should().Be(16500);
    }

    [Fact]
    public async Task ClaimQuery_Filter_StartDate()
    {
        // Arrange
        var owner = _fixture.Create<string>();
        var startDate = new DateTimeOffset(2023, 7, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateClaimsAndCerts(owner, 48, startDate);

        // Act
        var claims = await _repository.GetClaims(owner, new ClaimFilter()
        {
            Start = startDate.AddHours(48 - 4)
        });

        // Assert
        claims.Should().NotBeNull();
        claims.Should().HaveCount(4);
        claims.Sum(x => x.Quantity).Should().Be(1300);
    }

    [Fact]
    public async Task ClaimQuery_Filter_EndDate()
    {
        // Arrange
        var owner = _fixture.Create<string>();
        var startDate = new DateTimeOffset(2023, 7, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateClaimsAndCerts(owner, 48, startDate);

        // Act
        var claims = await _repository.GetClaims(owner, new ClaimFilter()
        {
            End = startDate.AddHours(4)
        });

        // Assert
        claims.Should().NotBeNull();
        claims.Should().HaveCount(4);
        claims.Sum(x => x.Quantity).Should().Be(1200);
    }

    [Fact]
    public async Task ClaimQuery_Filter_StartRange()
    {
        // Arrange
        var owner = _fixture.Create<string>();
        var startDate = new DateTimeOffset(2023, 7, 1, 0, 0, 0, TimeSpan.Zero);
        await CreateClaimsAndCerts(owner, 48, startDate);

        // Act
        var claims = await _repository.GetClaims(owner, new ClaimFilter()
        {
            Start = startDate.AddHours(10),
            End = startDate.AddHours(15)
        });

        // Assert
        claims.Should().NotBeNull();
        claims.Should().HaveCount(5);
        claims.Sum(x => x.Quantity).Should().Be(1750L);
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
        await _repository.InsertCertificate(certificate);

        // Act
        await _repository.InsertWalletAttribute(wallet.Id, assetIdWalletAttribute);
        var walletAttributeFromRepo = await _repository.GetWalletAttributes(wallet.Id, certificate.Id, certificate.RegistryName, new string[] { assetIdWalletAttribute.Key });

        // Assert
        var foundAttribute = walletAttributeFromRepo.Should().ContainSingle().Which;
        foundAttribute.Value.Should().Be(assetIdWalletAttribute.Value);
    }

    private async Task CreateClaimsAndCerts(string owner, int numberOfClaims, DateTimeOffset startDate)
    {
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
            await _repository.InsertWalletSlice(conSlice);

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
            await _repository.InsertWalletSlice(prodSlice);

            var claim = new Claim
            {
                Id = Guid.NewGuid(),
                ConsumptionSliceId = conSlice.Id,
                ProductionSliceId = prodSlice.Id,
                State = ClaimState.Claimed
            };
            await _repository.InsertClaim(claim);
        }
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

            await _repository.InsertWalletSlice(slice);
        }
    }
}
