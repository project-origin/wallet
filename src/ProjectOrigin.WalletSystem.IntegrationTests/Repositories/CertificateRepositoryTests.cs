using AutoFixture;
using Dapper;
using FluentAssertions;
using Npgsql;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.Server.Extensions;
using Xunit;

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
        var registry = await CreateRegistry();
        var attributes = new List<CertificateAttribute>
        {
            new ("AssetId", "571234567890123456"),
            new ("TechCode", "T070000"),
            new ("FuelCode", "F00000000")
        };
        var certificate = new Certificate(Guid.NewGuid(),
            registry.Id,
            DateTimeOffset.Now.ToUtcTime(),
            DateTimeOffset.Now.AddDays(1).ToUtcTime(),
            "DK1",
            GranularCertificateType.Production,
            attributes);

        // Act
        await _repository.InsertCertificate(certificate);

        // Assert
        var insertedCertificate = await _repository.GetCertificate(registry.Id, certificate.Id);
        insertedCertificate.Should().BeEquivalentTo(certificate);
    }

    [Fact]
    public async Task GetCertificate_ReturnsCertificate()
    {
        // Arrange
        var registry = await CreateRegistry();
        var certificate = await CreateCertificate(registry.Id);

        // Act
        var result = await _repository.GetCertificate(registry.Id, certificate.Id);

        // Assert
        result.Should().BeEquivalentTo(certificate);
    }

    [Fact]
    public async Task CreateSlice_InsertsSlice()
    {
        // Arrange
        var walletPosition = 1;
        var depositEndpointPosition = 1;
        var registry = await CreateRegistry();
        var certificate = await CreateCertificate(registry.Id);
        var wallet = await CreateWallet(_fixture.Create<string>());
        var depositEndpoint = await CreateDepositEndpoint(wallet, walletPosition);
        var slice = new Slice(Guid.NewGuid(), depositEndpoint.Id, depositEndpointPosition, registry.Id, certificate.Id, _fixture.Create<int>(), _fixture.Create<byte[]>());

        // Act
        await _repository.InsertSlice(slice);

        // Assert
        var insertedSlice = await _connection.QueryFirstOrDefaultAsync<Slice>("SELECT * FROM Slices WHERE Id = @id", new { slice.Id });
        insertedSlice.Should().BeEquivalentTo(slice);
    }

    [Fact]
    public async Task GetAllOwnedCertificates()
    {
        // Arrange
        var walletPosition = 1;
        var deposintEndpointPosition = 1;
        var registry = await CreateRegistry();
        var certificate1 = await CreateCertificate(registry.Id);
        var certificate2 = await CreateCertificate(registry.Id, GranularCertificateType.Consumption);
        var certificate3 = await CreateCertificate(registry.Id);
        var owner1 = _fixture.Create<string>();
        var wallet1 = await CreateWallet(owner1);
        var depositEndpoint1 = await CreateDepositEndpoint(wallet1, walletPosition);
        var depositEndpoint2 = await CreateDepositEndpoint(wallet1, walletPosition + 1);
        var owner2 = _fixture.Create<string>();
        var wallet2 = await CreateWallet(owner2);
        var depositEndpoint3 = await CreateDepositEndpoint(wallet2, walletPosition);
        //Wallet1
        var slice1 = new Slice(Guid.NewGuid(), depositEndpoint1.Id, deposintEndpointPosition, registry.Id, certificate1.Id, _fixture.Create<int>(),
            _fixture.Create<byte[]>());
        var slice2 = new Slice(Guid.NewGuid(), depositEndpoint1.Id, deposintEndpointPosition + 1, registry.Id, certificate1.Id, _fixture.Create<int>(),
            _fixture.Create<byte[]>());
        //Certficiate2
        var slice3 = new Slice(Guid.NewGuid(), depositEndpoint2.Id, deposintEndpointPosition, registry.Id, certificate2.Id, _fixture.Create<int>(),
            _fixture.Create<byte[]>());

        var sliceWithDifferentOwner = new Slice(Guid.NewGuid(), depositEndpoint3.Id, deposintEndpointPosition, registry.Id, certificate3.Id,
            _fixture.Create<int>(), _fixture.Create<byte[]>());

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

    [Fact]
    public async Task CreateSlice_InsertsReceivedSlice()
    {
        // Arrange
        var walletPosition = 1;
        var depositEndpointPosition = 1;
        var register = _fixture.Create<string>();
        var certificateId = Guid.NewGuid();
        var wallet = await CreateWallet(_fixture.Create<string>());
        var depositEndpoint = await CreateDepositEndpoint(wallet, walletPosition);
        var receivedSlice = new ReceivedSlice(Guid.NewGuid(), depositEndpoint.Id, depositEndpointPosition, register, certificateId, _fixture.Create<int>(), _fixture.Create<byte[]>());

        // Act
        await _repository.InsertReceivedSlice(receivedSlice);

        // Assert
        var insertedSlice = await _connection.QueryFirstOrDefaultAsync<ReceivedSlice>("SELECT * FROM ReceivedSlices WHERE Id = @id", new { receivedSlice.Id });
        insertedSlice.Should().BeEquivalentTo(receivedSlice);
    }

    [Fact]
    public async Task GetAllReceivedSlices()
    {
        var walletPosition = 1;
        var depositEndpointPosition = 1;
        var register = _fixture.Create<string>();
        var wallet1 = await CreateWallet(_fixture.Create<string>());
        var wallet2 = await CreateWallet(_fixture.Create<string>());
        var certificateId1 = Guid.NewGuid();
        var certificateId2 = Guid.NewGuid();
        var certificateId3 = Guid.NewGuid();
        var walletDepositEndpoint1 = await CreateDepositEndpoint(wallet1, walletPosition);
        var walletDepositEndpoint2 = await CreateDepositEndpoint(wallet2, walletPosition);
        var receivedSlice1 = new ReceivedSlice(Guid.NewGuid(), walletDepositEndpoint1.Id, depositEndpointPosition, register, certificateId1, _fixture.Create<int>(), _fixture.Create<byte[]>());
        var receivedSlice2 = new ReceivedSlice(Guid.NewGuid(), walletDepositEndpoint1.Id, depositEndpointPosition + 1, register, certificateId2, _fixture.Create<int>(), _fixture.Create<byte[]>());
        var receivedSlice3 = new ReceivedSlice(Guid.NewGuid(), walletDepositEndpoint2.Id, depositEndpointPosition, register, certificateId3, _fixture.Create<int>(), _fixture.Create<byte[]>());

        await _repository.InsertReceivedSlice(receivedSlice1);
        await _repository.InsertReceivedSlice(receivedSlice2);
        await _repository.InsertReceivedSlice(receivedSlice3);

        var slicesDb = await _repository.GetAllReceivedSlices();

        var receivedSliceIds = new[]
        {
            receivedSlice1.Id, receivedSlice2.Id, receivedSlice3.Id
        };

        var ids = slicesDb.Select(x => x.Id).ToList();
        ids.Should().Contain(receivedSliceIds);
    }

    [Fact]
    public async Task RemoveReceivedSlices()
    {
        var walletPosition = 1;
        var depositEndpointPosition = 1;
        var register = _fixture.Create<string>();
        var wallet1 = await CreateWallet(_fixture.Create<string>());
        var wallet2 = await CreateWallet(_fixture.Create<string>());
        var certificateId1 = Guid.NewGuid();
        var certificateId2 = Guid.NewGuid();
        var certificateId3 = Guid.NewGuid();
        var walletDepositEndpoint1 = await CreateDepositEndpoint(wallet1, walletPosition);
        var walletDepositEndpoint2 = await CreateDepositEndpoint(wallet2, walletPosition);
        var receivedSlice1 = new ReceivedSlice(Guid.NewGuid(), walletDepositEndpoint1.Id, depositEndpointPosition, register, certificateId1, _fixture.Create<int>(), _fixture.Create<byte[]>());
        var receivedSlice2 = new ReceivedSlice(Guid.NewGuid(), walletDepositEndpoint1.Id, depositEndpointPosition + 1, register, certificateId2, _fixture.Create<int>(), _fixture.Create<byte[]>());
        var receivedSlice3 = new ReceivedSlice(Guid.NewGuid(), walletDepositEndpoint1.Id, depositEndpointPosition, register, certificateId3, _fixture.Create<int>(), _fixture.Create<byte[]>());

        await _repository.InsertReceivedSlice(receivedSlice1);
        await _repository.InsertReceivedSlice(receivedSlice2);
        await _repository.InsertReceivedSlice(receivedSlice3);

        var slices = new List<ReceivedSlice>
        {
            receivedSlice1,
            receivedSlice2,
            receivedSlice3
        };

        await _repository.RemoveReceivedSlices(slices);

        var slicesDb = await _repository.GetReceivedSlices(new List<Guid> { receivedSlice1.Id, receivedSlice2.Id, receivedSlice3.Id });

        slicesDb.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveReceivedSlice()
    {
        var walletPosition = 1;
        var depositEndpointPosition = 1;
        var register = _fixture.Create<string>();
        var wallet1 = await CreateWallet(_fixture.Create<string>());
        var certificateId1 = Guid.NewGuid();
        var walletDepositEndpoint1 = await CreateDepositEndpoint(wallet1, walletPosition);
        var receivedSlice1 = new ReceivedSlice(Guid.NewGuid(), walletDepositEndpoint1.Id, depositEndpointPosition, register, certificateId1, _fixture.Create<int>(), _fixture.Create<byte[]>());
        await _repository.InsertReceivedSlice(receivedSlice1);

        await _repository.RemoveReceivedSlice(receivedSlice1);

        var slicesDb = await _repository.GetReceivedSlices(new List<Guid> { receivedSlice1.Id });
        slicesDb.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTop1ReceivedSlice()
    {
        var slicesInDb = await _repository.GetAllReceivedSlices();
        await _repository.RemoveReceivedSlices(slicesInDb.ToList());

        var walletPosition = 1;
        var depositEndpointPosition = 1;
        var register = _fixture.Create<string>();
        var wallet1 = await CreateWallet(_fixture.Create<string>());
        var certificateId1 = Guid.NewGuid();
        var walletDepositEndpoint1 = await CreateDepositEndpoint(wallet1, walletPosition);
        var receivedSlice1 = new ReceivedSlice(Guid.NewGuid(), walletDepositEndpoint1.Id, depositEndpointPosition, register, certificateId1, _fixture.Create<int>(), _fixture.Create<byte[]>());
        await _repository.InsertReceivedSlice(receivedSlice1);

        var receivedSlice = await _repository.GetTop1ReceivedSlice();

        receivedSlice.Should().BeEquivalentTo(receivedSlice1);
    }

    [Fact]
    public async Task GetTop1ReceivedSlice_WhenNoSlices_ReturnNull()
    {
        var slicesInDb = await _repository.GetAllReceivedSlices();
        await _repository.RemoveReceivedSlices(slicesInDb.ToList());

        var receivedSlice = await _repository.GetTop1ReceivedSlice();

        receivedSlice.Should().BeNull();
    }

    [Fact]
    public async Task InstertReceivedSlice_WhenInsertingTwoOfTheSameEntity_ExpectDatabaseException()
    {
        var walletPosition = 1;
        var depositEndpointPosition = 1;
        var register = _fixture.Create<string>();
        var wallet1 = await CreateWallet(_fixture.Create<string>());
        var certificateId1 = Guid.NewGuid();
        var walletDepositEndpoint1 = await CreateDepositEndpoint(wallet1, walletPosition);
        var receivedSlice1 = new ReceivedSlice(Guid.NewGuid(), walletDepositEndpoint1.Id, depositEndpointPosition, register, certificateId1, _fixture.Create<int>(), _fixture.Create<byte[]>());
        await _repository.InsertReceivedSlice(receivedSlice1);

        var act = async () => await _repository.InsertReceivedSlice(receivedSlice1);

        await act.Should().ThrowAsync<PostgresException>();
    }
}
