using AutoFixture;
using Dapper;
using FluentAssertions;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.Repositories;

public class CertificateRepositoryTests : AbstractRepositoryTests
{
    private readonly CertificateRepository _repository;

    public CertificateRepositoryTests(PostgresDatabaseFixture dbFixture) : base(dbFixture)
    {
        _repository = new CertificateRepository(Connection);
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
        var insertedCertificate = await Connection.QueryFirstOrDefaultAsync<Certificate>("SELECT * FROM Certificates WHERE Id = @id", new { certificate.Id });
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
        var wallet = await CreateWallet(Fixture.Create<string>());
        var walletSection = await CreateWalletSection(wallet, walletPosition);
        var slice = new Slice(Guid.NewGuid(), walletSection.Id, sectionPosition, registry.Id, certificate.Id, Fixture.Create<int>(), Fixture.Create<byte[]>(), SliceState.Unverified);

        // Act
        await _repository.InsertSlice(slice);

        // Assert
        var insertedSlice = await Connection.QueryFirstOrDefaultAsync<Slice>("SELECT * FROM Slices WHERE Id = @id", new { slice.Id });
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
}
