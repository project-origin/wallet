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
        _repository = new CertificateRepository(_connection);
    }

    [Fact]
    public async Task InsertCertificate_InsertsCertificate()
    {
        // Arrange
        var registry = await CreateRegistry();
        var certificate = new Certificate(Guid.NewGuid(), registry.Id);

        // Act
        await _repository.InsertCertificate(certificate);

        // Assert
        var insertedCertificate = await _connection.QueryFirstOrDefaultAsync<Certificate>("SELECT * FROM Certificates WHERE Id = @id", new { certificate.Id });
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
        var sectionPosition = 1;
        var registry = await CreateRegistry();
        var certificate = await CreateCertificate(registry.Id);
        var wallet = await CreateWallet(_fixture.Create<string>());
        var walletSection = await CreateWalletSection(wallet, walletPosition);
        var slice = new Slice(Guid.NewGuid(), walletSection.Id, sectionPosition, registry.Id, certificate.Id, _fixture.Create<int>(), _fixture.Create<byte[]>());

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
        var sectionPosition = 1;
        var registry = await CreateRegistry();
        var certificate1 = await CreateCertificate(registry.Id);
        var certificate2 = await CreateCertificate(registry.Id);
        var certificate3 = await CreateCertificate(registry.Id);
        var owner1 = _fixture.Create<string>();
        var wallet1 = await CreateWallet(owner1);
        var walletSection1 = await CreateWalletSection(wallet1, walletPosition);
        var walletSection2 = await CreateWalletSection(wallet1, walletPosition + 1);
        var owner2 = _fixture.Create<string>();
        var wallet2 = await CreateWallet(owner2);
        var walletSection3 = await CreateWalletSection(wallet2, walletPosition);
        //Wallet1
        var slice1 = new Slice(Guid.NewGuid(), walletSection1.Id, sectionPosition, registry.Id, certificate1.Id, _fixture.Create<int>(),
            _fixture.Create<byte[]>());
        var slice2 = new Slice(Guid.NewGuid(), walletSection1.Id, sectionPosition + 1, registry.Id, certificate1.Id, _fixture.Create<int>(),
            _fixture.Create<byte[]>());
        //Certficiate2
        var slice3 = new Slice(Guid.NewGuid(), walletSection2.Id, sectionPosition, registry.Id, certificate2.Id, _fixture.Create<int>(),
            _fixture.Create<byte[]>());

        var sliceWithDifferentOwner = new Slice(Guid.NewGuid(), walletSection3.Id, sectionPosition, registry.Id, certificate3.Id,
            _fixture.Create<int>(), _fixture.Create<byte[]>());

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

    [Fact]
    public async Task CreateSlice_InsertsReceivedSlice()
    {
        // Arrange
        var walletPosition = 1;
        var sectionPosition = 1;
        var register = new Fixture().Create<string>();
        var certificateId = Guid.NewGuid();
        var wallet = await CreateWallet(_fixture.Create<string>());
        var walletSection = await CreateWalletSection(wallet, walletPosition);
        var receivedSlice = new ReceivedSlice(Guid.NewGuid(), walletSection.Id, sectionPosition, register, certificateId, _fixture.Create<int>(), _fixture.Create<byte[]>());

        // Act
        await _repository.InsertReceivedSlice(receivedSlice);

        // Assert
        var insertedSlice = await _connection.QueryFirstOrDefaultAsync<ReceivedSlice>("SELECT * FROM ReceivedSlices WHERE Id = @id", new { receivedSlice.Id });
        insertedSlice.Should().BeEquivalentTo(receivedSlice);
    }
}
