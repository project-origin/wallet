using AutoFixture;
using FluentAssertions;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.Repositories;

public class WalletRepositoryTests : AbstractRepositoryTests
{
    private readonly WalletRepository _repository;

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

        using var connection = _dbFixture.GetConnectionFactory().CreateConnection();
        connection.Open();
        var repository = new WalletRepository(connection);

        // Act
        await repository.Create(wallet);

        // Assert
        var walletDb = await repository.GetWalletByOwner(subject);
        walletDb.Should().BeEquivalentTo(wallet);
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
        using var connection = _dbFixture.GetConnectionFactory().CreateConnection();
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
    public async Task Query_CreateDepositEndpoint_GetNextWalletPosition_Valid(int sections, int next)
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var wallet = new Wallet(
            Guid.NewGuid(),
            subject,
            _algorithm.GenerateNewPrivateKey()
            );
        using var connection = _dbFixture.GetConnectionFactory().CreateConnection();
        connection.Open();
        var repository = new WalletRepository(connection);
        await repository.Create(wallet);

        for (int position = 1; position <= sections; position++)
        {
            await repository.CreateDepositEndpoint(wallet.Id, string.Empty);
        }

        // Act
        var walletResponse = await repository.GetNextNumberForId(wallet.Id);

        // Assert
        walletResponse.Should().Be(next);
    }

    [Fact]
    public async Task GetWalletDepositEndpointFromPublicKey_Success()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var wallet = await CreateWallet(subject);
        var depositEndpoint1 = await CreateDepositEndpoint(wallet);
        var depositEndpoint2 = await CreateDepositEndpoint(wallet);
        var depositEndpoint3 = await CreateDepositEndpoint(wallet);

        // Act
        var publicKey = wallet.PrivateKey.Derive(2).Neuter();
        var depositEndpoint = await _repository.GetDepositEndpointFromPublicKey(publicKey);

        // Assert
        depositEndpoint.Should().NotBeNull();
        depositEndpoint!.Id.Should().Be(depositEndpoint2.Id);
    }

    [Fact]
    public async Task GetWalletDepositEndpointFromPublicKey_ReturnNull()
    {
        // Arrange
        var publicKey = _algorithm.GenerateNewPrivateKey().Derive(1).Neuter();

        // Act
        var depositEndpoint = await _repository.GetDepositEndpointFromPublicKey(publicKey);

        // Assert
        depositEndpoint.Should().BeNull();
    }

    [Fact]
    public async Task GetReceiverDepositEndpoint()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var depositEndpoint = await CreateReceiverDepositEndpoint(subject, _fixture.Create<string>(), _fixture.Create<string>());

        // Act
        var deDb = await _repository.GetDepositEndpoint(depositEndpoint.Id);

        // Assert
        deDb.Should().NotBeNull();
        deDb.WalletPosition.Should().BeNull();
        deDb.WalletId.Should().BeNull();
    }

    [Fact]
    public async Task GetWallet()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var wallet = new Wallet(
            Guid.NewGuid(),
            subject,
            _algorithm.GenerateNewPrivateKey()
            );
        using var connection = _dbFixture.GetConnectionFactory().CreateConnection();
        connection.Open();
        var repository = new WalletRepository(connection);
        await repository.Create(wallet);

        // Act
        var walletResponse = await repository.GetWallet(wallet.Id);

        // Assert
        walletResponse.Should().BeEquivalentTo(wallet);
    }


    [Fact]
    public async Task GetNextNumberForIdTest()
    {
        // Arrange
        using var connection = _dbFixture.GetConnectionFactory().CreateConnection();
        connection.Open();
        var repository = new WalletRepository(connection);
        var id = Guid.NewGuid();

        // Act
        await repository.GetNextNumberForId(id);
        await repository.GetNextNumberForId(id);
        var number = await repository.GetNextNumberForId(id);

        // Assert
        number.Should().Be(3);
    }
}
