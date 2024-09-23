using AutoFixture;
using Npgsql;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Repositories;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ProjectOrigin.Vault.Extensions;
using Xunit;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.Vault.Tests.TestClassFixtures;

namespace ProjectOrigin.Vault.Tests.Repositories;

public abstract class AbstractRepositoryTests : IClassFixture<PostgresDatabaseFixture>, IDisposable
{
    protected PostgresDatabaseFixture _dbFixture;
    protected IHDAlgorithm _algorithm;
    protected IDbConnection _connection;
    protected Fixture _fixture;
    private bool _disposed = false;

    protected AbstractRepositoryTests(PostgresDatabaseFixture dbFixture)
    {
        _dbFixture = dbFixture;
        _algorithm = new Secp256k1Algorithm();
        _connection = CreateConnection();
        _fixture = new Fixture();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _connection.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~AbstractRepositoryTests()
    {
        Dispose(false);
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

    protected async Task<Certificate> CreateCertificate(string registryName, GranularCertificateType type = GranularCertificateType.Production, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)
    {
        using var connection = CreateConnection();
        var certificateRepository = new CertificateRepository(connection);

        var attributes = new List<CertificateAttribute>
        {
            new(){ Key="TechCode", Value="T070000", Type=CertificateAttributeType.ClearText},
            new(){ Key="FuelCode", Value="F00000000", Type=CertificateAttributeType.ClearText},
        };
        var certificate = new Certificate
        {
            Id = Guid.NewGuid(),
            RegistryName = registryName,
            StartDate = startDate?.ToUtcTime() ?? DateTimeOffset.Now.ToUtcTime(),
            EndDate = endDate?.ToUtcTime() ?? DateTimeOffset.Now.AddHours(1).ToUtcTime(),
            GridArea = "DK1",
            CertificateType = type,
            Attributes = attributes,
            Withdrawn = false
        };
        await certificateRepository.InsertCertificate(certificate);

        return certificate;
    }
}
