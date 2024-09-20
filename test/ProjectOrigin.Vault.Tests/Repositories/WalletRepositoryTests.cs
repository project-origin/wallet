using AutoFixture;
using FluentAssertions;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Repositories;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.Vault.Tests.Repositories;

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
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            Owner = subject,
            PrivateKey = _algorithm.GenerateNewPrivateKey()
        };

        using var connection = _dbFixture.GetConnectionFactory().CreateConnection();
        connection.Open();
        var repository = new WalletRepository(connection);

        // Act
        await repository.Create(wallet);

        // Assert
        var walletDb = await repository.GetWallet(subject);
        walletDb.Should().BeEquivalentTo(wallet);
    }

    [Fact]
    public async Task Query_GetWallet()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            Owner = subject,
            PrivateKey = _algorithm.GenerateNewPrivateKey()
        };
        using var connection = _dbFixture.GetConnectionFactory().CreateConnection();
        connection.Open();
        var repository = new WalletRepository(connection);
        await repository.Create(wallet);

        // Act
        var walletResponse = await repository.GetWallet(subject);

        // Assert
        walletResponse.Should().NotBeNull();
        walletResponse!.Owner.Should().Be(subject);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(3, 4)]
    public async Task Query_CreateWalletEndpoint_GetNextWalletPosition_Valid(int endpoints, int next)
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            Owner = subject,
            PrivateKey = _algorithm.GenerateNewPrivateKey()
        };
        using var connection = _dbFixture.GetConnectionFactory().CreateConnection();
        connection.Open();
        var repository = new WalletRepository(connection);
        await repository.Create(wallet);

        for (int position = 1; position <= endpoints; position++)
        {
            await repository.CreateWalletEndpoint(wallet.Id);
        }

        // Act
        var walletResponse = await repository.GetNextNumberForId(wallet.Id);

        // Assert
        walletResponse.Should().Be(next);
    }

    [Fact]
    public async Task GetWalletEndpointFromPublicKey_Success()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var wallet = await CreateWallet(subject);
        var endpoint1 = await CreateWalletEndpoint(wallet);
        var endpoint2 = await CreateWalletEndpoint(wallet);
        var endpoint3 = await CreateWalletEndpoint(wallet);

        // Act
        var publicKey = wallet.PrivateKey.Derive(2).Neuter();
        var endpoint = await _repository.GetWalletEndpoint(publicKey);

        // Assert
        endpoint.Should().NotBeNull();
        endpoint!.Id.Should().Be(endpoint2.Id);
    }

    [Fact]
    public async Task GetWalletEndpointFromPublicKey_ReturnNull()
    {
        // Arrange
        var publicKey = _algorithm.GenerateNewPrivateKey().Derive(1).Neuter();

        // Act
        var endpoint = await _repository.GetWalletEndpoint(publicKey);

        // Assert
        endpoint.Should().BeNull();
    }

    [Fact]
    public async Task GetExternalEndpoint()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var endpoint = await CreateExternalEndpoint(subject, _fixture.Create<string>(), _fixture.Create<string>());

        // Act
        var deDb = await _repository.GetExternalEndpoint(endpoint.Id);

        // Assert
        deDb.Should().NotBeNull();
    }

    [Fact]
    public async Task GetWallet()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            Owner = subject,
            PrivateKey = _algorithm.GenerateNewPrivateKey()
        };
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
