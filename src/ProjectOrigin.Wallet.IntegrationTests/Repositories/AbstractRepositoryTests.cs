using AutoFixture;
using Dapper;
using Npgsql;
using ProjectOrigin.Wallet.Server.Database;
using ProjectOrigin.Wallet.Server.Database.Mapping;
using ProjectOrigin.Wallet.Server.HDWallet;
using ProjectOrigin.Wallet.Server.Models;
using ProjectOrigin.Wallet.Server.Repositories;
using System;
using System.Data;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.Wallet.IntegrationTests.Repositories;

public abstract class AbstractRepositoryTests : IClassFixture<PostgresDatabaseFixture>, IDisposable
{
    protected PostgresDatabaseFixture _dbFixture;
    protected IHDAlgorithm _algorithm;
    protected IDbConnection _connection;
    protected Fixture _fixture;

    public AbstractRepositoryTests(PostgresDatabaseFixture dbFixture)
    {
        DatabaseUpgrader.Upgrade(dbFixture.ConnectionString);

        this._dbFixture = dbFixture;
        this._algorithm = new Secp256k1Algorithm();
        this._connection = CreateConnection();
        this._fixture = new Fixture();

        SqlMapper.AddTypeHandler<IHDPrivateKey>(new HDPrivateKeyTypeHandler(this._algorithm));
        SqlMapper.AddTypeHandler<IHDPublicKey>(new HDPublicKeyTypeHandler(this._algorithm));
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private IDbConnection CreateConnection()
    {
        var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        connection.Open();
        return connection;
    }

    protected async Task<Registry> CreateRegistry()
    {
        var registry = new Registry(Guid.NewGuid(), _fixture.Create<string>());

        using (var connection = CreateConnection())
        {
            await connection.ExecuteAsync("INSERT INTO Registries(Id, Name) VALUES (@id, @name)", registry);
        }

        return registry;
    }

    protected async Task<OwnerWallet> CreateWallet(string owner)
    {
        var walletRepository = new WalletRepository(CreateConnection());

        var wallet = new OwnerWallet(Guid.NewGuid(), owner, _algorithm.GenerateNewPrivateKey());
        await walletRepository.Create(wallet);

        return wallet;
    }

    protected async Task<WalletSection> CreateWalletSection(OwnerWallet wallet, int position)
    {
        var walletRepository = new WalletRepository(CreateConnection());

        var publicKey = wallet.PrivateKey.Derive(position).PublicKey;
        var walletSection = new WalletSection(Guid.NewGuid(), wallet.Id, position, publicKey);
        await walletRepository.CreateSection(walletSection);

        return walletSection;
    }

    protected async Task<Certificate> CreateCertificate(Guid registryId, bool loaded)
    {
        var certificate = new Certificate(Guid.NewGuid(), registryId, loaded);

        using (var connection = CreateConnection())
        {
            await connection.ExecuteAsync("INSERT INTO Certificates(Id, RegistryId, Loaded) VALUES (@id, @registryId, @loaded)", certificate);
        }

        return certificate;
    }


}
