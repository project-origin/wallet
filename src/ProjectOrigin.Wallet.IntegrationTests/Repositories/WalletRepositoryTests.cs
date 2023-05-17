using Dapper;
using FluentAssertions;
using ProjectOrigin.Wallet.Server.Database;
using ProjectOrigin.Wallet.Server.Database.Mapping;
using ProjectOrigin.Wallet.Server.HDWallet;
using ProjectOrigin.Wallet.Server.Models;
using ProjectOrigin.Wallet.Server.Repositories;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.Wallet.IntegrationTests.Repositories;

public class WalletRepositoryTests : IClassFixture<PostgresDatabaseFixture>
{
    private PostgresDatabaseFixture _dbFixture;
    private Secp256k1Algorithm _algorithm;

    public WalletRepositoryTests(PostgresDatabaseFixture dbFixture)
    {
        this._algorithm = new Secp256k1Algorithm();
        this._dbFixture = dbFixture;
        DatabaseUpgrader.Upgrade(dbFixture.ConnectionString);

        SqlMapper.AddTypeHandler<IHDPrivateKey>(new HDPrivateKeyTypeHandler(this._algorithm));
        SqlMapper.AddTypeHandler<IHDPublicKey>(new HDPublicKeyTypeHandler(this._algorithm));
    }

    [Fact]
    public async Task Create_InsertsWallet()
    {
        var subject = Guid.NewGuid().ToString();

        // Arrange
        var wallet = new OwnerWallet(
            Guid.NewGuid(),
            subject,
            _algorithm.GenerateNewPrivateKey()
            );

        using var connection = new DbConnectionFactory(_dbFixture.ConnectionString).CreateConnection();
        connection.Open();
        WalletRepository repository = new WalletRepository(connection);

        // Act
        await repository.Create(wallet);

        // Assert
        var walletDb = await connection.QueryAsync<OwnerWallet>("SELECT * FROM Wallets where Owner = @Owner", new { Owner = subject });
        walletDb.Should().HaveCount(1);
        walletDb.Single().Owner.Should().Be(subject);
    }

    [Fact]
    public async Task Query_GetWallet()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var wallet = new OwnerWallet(
            Guid.NewGuid(),
            subject,
            _algorithm.GenerateNewPrivateKey()
            );
        using var connection = new DbConnectionFactory(_dbFixture.ConnectionString).CreateConnection();
        connection.Open();
        WalletRepository repository = new WalletRepository(connection);
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
    public async Task Query_CreateSection_GetNextWalletPosition_Valid(int sections, int next)
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var wallet = new OwnerWallet(
            Guid.NewGuid(),
            subject,
            _algorithm.GenerateNewPrivateKey()
            );
        using var connection = new DbConnectionFactory(_dbFixture.ConnectionString).CreateConnection();
        connection.Open();
        WalletRepository repository = new WalletRepository(connection);
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
    public async Task Query_GetSectionPublicKey_Success()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var wallet = new OwnerWallet(
            Guid.NewGuid(),
            subject,
            _algorithm.GenerateNewPrivateKey()
            );
        var targetSectionId = Guid.NewGuid();

        using var connection = new DbConnectionFactory(_dbFixture.ConnectionString).CreateConnection();
        connection.Open();
        WalletRepository repository = new WalletRepository(connection);
        await repository.Create(wallet);
        await repository.CreateSection(new WalletSection(Guid.NewGuid(), wallet.Id, 1, wallet.PrivateKey.Derive(1).PublicKey));
        await repository.CreateSection(new WalletSection(targetSectionId, wallet.Id, 2, wallet.PrivateKey.Derive(2).PublicKey));
        await repository.CreateSection(new WalletSection(Guid.NewGuid(), wallet.Id, 3, wallet.PrivateKey.Derive(3).PublicKey));

        // Act
        var publicKey = wallet.PrivateKey.Derive(2).PublicKey;
        var section = await repository.GetWalletSection(publicKey);

        // Assert
        section.Should().NotBeNull();
        section!.Id.Should().Be(targetSectionId);
    }
}
