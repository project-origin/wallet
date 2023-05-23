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
    protected PostgresDatabaseFixture DbFixture;
    protected IHDAlgorithm Algorithm;
    protected IDbConnection Connection;
    protected Fixture Fixture;

    protected AbstractRepositoryTests(PostgresDatabaseFixture dbFixture)
    {
        DbFixture = dbFixture;
        Algorithm = new Secp256k1Algorithm();
        Connection = CreateConnection();
        Fixture = new Fixture();

        SqlMapper.AddTypeHandler(new HDPrivateKeyTypeHandler(Algorithm));
        SqlMapper.AddTypeHandler(new HDPublicKeyTypeHandler(Algorithm));
    }

    public void Dispose()
    {
        Connection.Dispose();
    }

    private IDbConnection CreateConnection()
    {
        var connection = new NpgsqlConnection(DbFixture.ConnectionString);
        connection.Open();
        return connection;
    }

    protected async Task<Registry> CreateRegistry()
    {
        using var connection = CreateConnection();
        var registryRepository = new RegistryRepository(connection);

        var registry = new Registry(Guid.NewGuid(), Fixture.Create<string>());
        await registryRepository.InsertRegistry(registry);

        return registry;
    }

    protected async Task<Wallet> CreateWallet(string owner)
    {
        using var connection = CreateConnection();
        var walletRepository = new WalletRepository(connection);

        var wallet = new Wallet(Guid.NewGuid(), owner, Algorithm.GenerateNewPrivateKey());
        await walletRepository.Create(wallet);

        return wallet;
    }

    protected async Task<WalletSection> CreateWalletSection(Wallet wallet, int position)
    {
        using var connection = CreateConnection();
        var walletRepository = new WalletRepository(connection);

        var publicKey = wallet.PrivateKey.Derive(position).PublicKey;
        var walletSection = new WalletSection(Guid.NewGuid(), wallet.Id, position, publicKey);
        await walletRepository.CreateSection(walletSection);

        return walletSection;
    }

    protected async Task<Certificate> CreateCertificate(Guid registryId)
    {
        using var connection = CreateConnection();
        var certificateRepository = new CertificateRepository(connection);

        var certificate = new Certificate(Guid.NewGuid(), registryId);
        await certificateRepository.InsertCertificate(certificate);

        return certificate;
    }
}
