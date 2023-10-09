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
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;

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

    protected async Task<Wallet> CreateWallet(string owner)
    {
        using var connection = CreateConnection();
        var walletRepository = new WalletRepository(connection);

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            Owner = owner,
            PrivateKey = _algorithm.GenerateNewPrivateKey()
        };
        await walletRepository.Create(wallet);

        return wallet;
    }

    protected async Task<WalletEndpoint> CreateWalletEndpoint(Wallet wallet)
    {
        using var connection = CreateConnection();
        var walletRepository = new WalletRepository(connection);
        return await walletRepository.CreateWalletEndpoint(wallet.Id);
    }

    protected async Task<ExternalEndpoint> CreateExternalEndpoint(string owner, string referenceText, string endpoint)
    {
        using var connection = CreateConnection();
        var walletRepository = new WalletRepository(connection);

        var key = _algorithm.GenerateNewPrivateKey();
        var publicKey = key.Derive(_fixture.Create<int>()).Neuter();
        return await walletRepository.CreateExternalEndpoint(owner, publicKey, referenceText, endpoint);
    }

    protected async Task<Certificate> CreateCertificate(string registryName, GranularCertificateType type = GranularCertificateType.Production, DateTimeOffset? startDate = null)
    {
        using var connection = CreateConnection();
        var certificateRepository = new CertificateRepository(connection);

        var attributes = new List<CertificateAttribute>
        {
            new(){ Key="AssetId", Value="571234567890123456"},
            new(){ Key="TechCode", Value="T070000"},
            new(){ Key="FuelCode", Value="F00000000"},
        };
        var certificate = new Certificate
        {
            Id = Guid.NewGuid(),
            RegistryName = registryName,
            StartDate = startDate?.ToUtcTime() ?? DateTimeOffset.Now.ToUtcTime(),
            EndDate = startDate?.AddHours(1).ToUtcTime() ?? DateTimeOffset.Now.AddDays(1).ToUtcTime(),
            GridArea = "DK1",
            CertificateType = type,
            Attributes = attributes
        };
        await certificateRepository.InsertCertificate(certificate);

        return certificate;
    }
}
