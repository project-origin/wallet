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
    public async Task InsertRegistry_InsertsRegistry()
    {
        // Arrange
        var registry = Fixture.Create<Registry>();

        // Act
        await _repository.InsertRegistry(registry);

        // Assert
        var insertedRegistry = await Connection.QueryFirstOrDefaultAsync<Registry>("SELECT * FROM Registries WHERE Id = @id", new { registry.Id });
        insertedRegistry.Should().BeEquivalentTo(registry);
    }

    [Fact]
    public async Task GetRegistryFromName_ReturnsRegistry()
    {
        // Arrange
        var registry = await CreateRegistry();

        // Act
        var result = await _repository.GetRegistryFromName(registry.Name);

        // Assert
        result.Should().BeEquivalentTo(registry);
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
        insertedCertificate.Should().BeEquivalentTo(certificate);
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
        result.Should().BeEquivalentTo(certificate);
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
        insertedSlice.Should().BeEquivalentTo(slice);
    }
}
