using AutoFixture;
using Dapper;
using FluentAssertions;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.Repositories;

public class WalletRepositoryTests : AbstractRepositoryTests
{
    private WalletRepository _repository;

    public WalletRepositoryTests(PostgresDatabaseFixture dbFixture) : base(dbFixture)
    {
        _repository = new WalletRepository(_connection);
    }

    [Fact]
    public async Task Create_InsertsWallet()
    {
        var subject = Guid.NewGuid().ToString();

        // Arrange
        var wallet = new Wallet(
            Guid.NewGuid(),
            subject,
            _algorithm.GenerateNewPrivateKey()
            );

        using var connection = new DbConnectionFactory(_dbFixture.ConnectionString).CreateConnection();
        connection.Open();
        var repository = new WalletRepository(connection);

        // Act
        await repository.Create(wallet);

        // Assert
        var walletDb = await connection.QueryAsync<Wallet>("SELECT * FROM Wallets where Owner = @Owner", new { Owner = subject });
        walletDb.Should().HaveCount(1);
        walletDb.Single().Owner.Should().Be(subject);
    }

    [Fact]
    public async Task Query_GetWallet()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var wallet = new Wallet(
            Guid.NewGuid(),
            subject,
            _algorithm.GenerateNewPrivateKey()
            );
        using var connection = new DbConnectionFactory(_dbFixture.ConnectionString).CreateConnection();
        connection.Open();
        var repository = new WalletRepository(connection);
        await repository.Create(wallet);

        // Act
        var walletResponse = await repository.GetWalletByOwner(subject);

        // Assert
        walletResponse.Should().NotBeNull();
        walletResponse!.Owner.Should().Be(subject);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(3, 4)]
    public async Task Query_CreateSection_GetNextWalletPosition_Valid(int sections, int next)
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var wallet = new Wallet(
            Guid.NewGuid(),
            subject,
            _algorithm.GenerateNewPrivateKey()
            );
        using var connection = new DbConnectionFactory(_dbFixture.ConnectionString).CreateConnection();
        connection.Open();
        var repository = new WalletRepository(connection);
        await repository.Create(wallet);

        for (int position = 1; position <= sections; position++)
        {
            await repository.CreateSection(new WalletSection(Guid.NewGuid(), wallet.Id, position, wallet.PrivateKey.Derive(position).PublicKey));
        }

        // Act
        var walletResponse = await repository.GetNextWalletPosition(wallet.Id);

        // Assert
        walletResponse.Should().Be(next);
    }

    [Fact]
    public async Task GetWalletSectionFromPublicKey_Success()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var wallet = await CreateWallet(subject);
        var section1 = await CreateWalletSection(wallet, 1);
        var section2 = await CreateWalletSection(wallet, 2);
        var section3 = await CreateWalletSection(wallet, 3);

        // Act
        var publicKey = wallet.PrivateKey.Derive(2).PublicKey;
        var section = await _repository.GetWalletSectionFromPublicKey(publicKey);

        // Assert
        section.Should().NotBeNull();
        section!.Id.Should().Be(section2.Id);
    }

    [Fact]
    public async Task GetWalletSectionFromPublicKey_ReturnNull()
    {
        // Arrange
        var publicKey = _algorithm.GenerateNewPrivateKey().Derive(1).PublicKey;

        // Act
        var section = await _repository.GetWalletSectionFromPublicKey(publicKey);

        // Assert
        section.Should().BeNull();
    }
}
