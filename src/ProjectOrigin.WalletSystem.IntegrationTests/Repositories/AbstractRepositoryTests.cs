using AutoFixture;
using Dapper;
using Npgsql;
using ProjectOrigin.WalletSystem.Server.Database.Mapping;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.Server.Extensions;
using Xunit;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;

namespace ProjectOrigin.WalletSystem.IntegrationTests.Repositories;

public abstract class AbstractRepositoryTests : IClassFixture<PostgresDatabaseFixture>, IDisposable
{
    protected PostgresDatabaseFixture _dbFixture;
    protected IHDAlgorithm _algorithm;
    protected IDbConnection _connection;
    protected Fixture _fixture;

    protected AbstractRepositoryTests(PostgresDatabaseFixture dbFixture)
    {
        _dbFixture = dbFixture;
        _algorithm = new Secp256k1Algorithm();
        _connection = CreateConnection();
        _fixture = new Fixture();

        SqlMapper.AddTypeHandler(new HDPrivateKeyTypeHandler(_algorithm));
        SqlMapper.AddTypeHandler(new HDPublicKeyTypeHandler(_algorithm));
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

    protected async Task<RegistryModel> CreateRegistry()
    {
        using var connection = CreateConnection();
        var registryRepository = new RegistryRepository(connection);

        var registry = new RegistryModel(Guid.NewGuid(), _fixture.Create<string>());
        await registryRepository.InsertRegistry(registry);

        return registry;
    }

    protected async Task<Wallet> CreateWallet(string owner)
    {
        using var connection = CreateConnection();
        var walletRepository = new WalletRepository(connection);

        var wallet = new Wallet(Guid.NewGuid(), owner, _algorithm.GenerateNewPrivateKey());
        await walletRepository.Create(wallet);

        return wallet;
    }

    protected async Task<WalletSection> CreateWalletSection(Wallet wallet, int position)
    {
        using var connection = CreateConnection();
        var walletRepository = new WalletRepository(connection);

        var publicKey = wallet.PrivateKey.Derive(position).Neuter();
        var walletSection = new WalletSection(Guid.NewGuid(), wallet.Id, position, publicKey);
        await walletRepository.CreateSection(walletSection);

        return walletSection;
    }

    protected async Task<Certificate> CreateCertificate(Guid registryId, GranularCertificateType type = GranularCertificateType.Production)
    {
        using var connection = CreateConnection();
        var certificateRepository = new CertificateRepository(connection);

        var attributes = new List<CertificateAttribute>
        {
            new ("AssetId", "571234567890123456"),
            new ("TechCode", "T070000"),
            new ("FuelCode", "F00000000")
        };
        var certificate = new Certificate(Guid.NewGuid(), registryId, DateTimeOffset.Now.ToUtcTime(), DateTimeOffset.Now.AddDays(1).ToUtcTime(), "DK1", type, attributes);
        await certificateRepository.InsertCertificate(certificate);

        return certificate;
    }
}
