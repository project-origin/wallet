using AutoFixture;
using Dapper;
using Npgsql;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Database.Mapping;
using ProjectOrigin.WalletSystem.Server.HDWallet;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;
using System;
using System.Data;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.Repositories;

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

    protected async Task<Wallet> CreateWallet(string owner)
    {
        var walletRepository = new WalletRepository(CreateConnection());

        var wallet = new Wallet(Guid.NewGuid(), owner, _algorithm.GenerateNewPrivateKey());
        await walletRepository.Create(wallet);

        return wallet;
    }

    protected async Task<WalletSection> CreateWalletSection(Wallet wallet, int position)
    {
        var walletRepository = new WalletRepository(CreateConnection());

        var publicKey = wallet.PrivateKey.Derive(position).PublicKey;
        var walletSection = new WalletSection(Guid.NewGuid(), wallet.Id, position, publicKey);
        await walletRepository.CreateSection(walletSection);

        return walletSection;
    }

    protected async Task<Certificate> CreateCertificate(Guid registryId, CertificateState state)
    {
        var certificate = new Certificate(Guid.NewGuid(), registryId, state);

        using (var connection = CreateConnection())
        {
            await connection.ExecuteAsync("INSERT INTO Certificates(Id, RegistryId, State) VALUES (@id, @registryId, @state)", certificate);
        }

        return certificate;
    }
}
