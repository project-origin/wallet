using Dapper;
using FluentAssertions;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Database.Mapping;
using ProjectOrigin.WalletSystem.Server.HDWallet;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.Repositories;

public class WalletRepositoryTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly Secp256k1Algorithm _algorithm;

    public WalletRepositoryTests(PostgresDatabaseFixture dbFixture)
    {
        _algorithm = new Secp256k1Algorithm();
        _dbFixture = dbFixture;

        SqlMapper.AddTypeHandler(new HDPrivateKeyTypeHandler(_algorithm));
        SqlMapper.AddTypeHandler(new HDPublicKeyTypeHandler(_algorithm));
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
}
