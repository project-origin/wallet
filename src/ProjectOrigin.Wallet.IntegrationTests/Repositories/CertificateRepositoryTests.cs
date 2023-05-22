using AutoFixture;
using Dapper;
using FluentAssertions;
using ProjectOrigin.Wallet.Server.Models;
using ProjectOrigin.Wallet.Server.Repositories;
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.Wallet.IntegrationTests.Repositories;

public class CertificateRepositoryTest : AbstractRepositoryTests
{
    private CertificateRepository _repository;

    public CertificateRepositoryTest(PostgresDatabaseFixture dbFixture) : base(dbFixture)
    {
        _repository = new CertificateRepository(_connection);
    }

    [Fact]
    public async Task InsertRegistry_InsertsRegistry()
    {
        // Arrange
        var registry = _fixture.Create<Registry>();

        // Act
        await _repository.InsertRegistry(registry);

        // Assert
        var insertedRegistry = await _connection.QueryFirstOrDefaultAsync<Registry>("SELECT * FROM Registries WHERE Id = @id", new { registry.Id });
        insertedRegistry.Should().NotBeNull();
        insertedRegistry.Id.Should().Be(registry.Id);
        insertedRegistry.Name.Should().Be(registry.Name);
    }

    [Fact]
    public async Task GetRegistryFromName_ReturnsRegistry()
    {
        // Arrange
        var registry = await CreateRegistry();

        // Act
        var result = await _repository.GetRegistryFromName(registry.Name);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(registry.Id);
        result.Name.Should().Be(registry.Name);
    }

    [Fact]
    public async Task InsertCertificate_InsertsCertificate()
    {
        // Arrange
        var registry = await CreateRegistry();
        var certificate = new Certificate(Guid.NewGuid(), registry.Id, CertificateState.Invalid);

        // Act
        await _repository.InsertCertificate(certificate);

        // Assert
        var insertedCertificate = await _connection.QueryFirstOrDefaultAsync<Certificate>("SELECT * FROM Certificates WHERE Id = @id", new { certificate.Id });
        insertedCertificate.Should().NotBeNull();
        insertedCertificate.Id.Should().Be(certificate.Id);
        insertedCertificate.RegistryId.Should().Be(certificate.RegistryId);
        insertedCertificate.State.Should().Be(certificate.State);
    }

    [Fact]
    public async Task GetCertificate_ReturnsCertificate()
    {
        // Arrange
        var registry = await CreateRegistry();
        var certificate = await CreateCertificate(registry.Id, CertificateState.Inserted);

        // Act
        var result = await _repository.GetCertificate(registry.Id, certificate.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(certificate.Id);
        result.RegistryId.Should().Be(certificate.RegistryId);
        result.State.Should().Be(certificate.State);
    }

    [Fact]
    public async Task CreateSlice_InsertsSlice()
    {
        // Arrange
        var walletPosition = 1;
        var sectionPosition = 1;
        var registry = await CreateRegistry();
        var certificate = await CreateCertificate(registry.Id, CertificateState.Inserted);
        var wallet = await CreateWallet(_fixture.Create<string>());
        var walletSection = await CreateWalletSection(wallet, walletPosition);
        var slice = new Slice(Guid.NewGuid(), walletSection.Id, sectionPosition, registry.Id, certificate.Id, _fixture.Create<int>(), _fixture.Create<byte[]>(), SliceState.Unverified);

        // Act
        await _repository.InsertSlice(slice);

        // Assert
        var insertedSlice = await _connection.QueryFirstOrDefaultAsync<Slice>("SELECT * FROM Slices WHERE Id = @id", new { slice.Id });
        insertedSlice.Should().NotBeNull();
        insertedSlice.Id.Should().Be(slice.Id);
        insertedSlice.WalletSectionId.Should().Be(slice.WalletSectionId);
        insertedSlice.WalletSectionPosition.Should().Be(slice.WalletSectionPosition);
        insertedSlice.RegistryId.Should().Be(slice.RegistryId);
        insertedSlice.CertificateId.Should().Be(slice.CertificateId);
        insertedSlice.Quantity.Should().Be(slice.Quantity);
        Assert.True(slice.RandomR.SequenceEqual(insertedSlice.RandomR));
        insertedSlice.State.Should().Be(slice.State);
    }

    [Fact]
    public async Task GetAllOwnedCertificates()
    {
        // Arrange
        var walletPosition = 1;
        var sectionPosition = 1;
        var registry = await CreateRegistry();
        var certificate1 = await CreateCertificate(registry.Id, CertificateState.Inserted);
        var certificate2 = await CreateCertificate(registry.Id, CertificateState.Inserted);
        var certificate3 = await CreateCertificate(registry.Id, CertificateState.Inserted);
        var owner1 = _fixture.Create<string>();
        var wallet1 = await CreateWallet(owner1);
        var walletSection1 = await CreateWalletSection(wallet1, walletPosition);
        var walletSection2 = await CreateWalletSection(wallet1, walletPosition + 1);
        var owner2 = _fixture.Create<string>();
        var wallet2 = await CreateWallet(owner2);
        var walletSection3 = await CreateWalletSection(wallet2, walletPosition);
        //Wallet1
        var slice1 = new Slice(Guid.NewGuid(), walletSection1.Id, sectionPosition, registry.Id, certificate1.Id, _fixture.Create<int>(),
            _fixture.Create<byte[]>(), SliceState.Unverified);
        var slice2 = new Slice(Guid.NewGuid(), walletSection1.Id, sectionPosition + 1, registry.Id, certificate1.Id, _fixture.Create<int>(),
            _fixture.Create<byte[]>(), SliceState.Unverified);
        //Certficiate2
        var slice3 = new Slice(Guid.NewGuid(), walletSection2.Id, sectionPosition, registry.Id, certificate2.Id, _fixture.Create<int>(),
            _fixture.Create<byte[]>(), SliceState.Unverified);

        var sliceWithDifferentOwner = new Slice(Guid.NewGuid(), walletSection3.Id, sectionPosition, registry.Id, certificate3.Id,
            _fixture.Create<int>(), _fixture.Create<byte[]>(), SliceState.Unverified);

        await _repository.InsertSlice(slice1);
        await _repository.InsertSlice(slice2);
        await _repository.InsertSlice(slice3);
        await _repository.InsertSlice(sliceWithDifferentOwner);

        var certificates = await _repository.GetAllOwnedCertificates(owner1);

        certificates.Should().HaveCount(2).And.Satisfy(
            c => c.Id == certificate1.Id && c.Quantity == slice1.Quantity + slice2.Quantity,
            c => c.Id == certificate2.Id && c.Quantity == slice3.Quantity
        );
    }
}
