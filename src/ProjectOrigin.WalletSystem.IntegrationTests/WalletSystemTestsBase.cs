using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
using System;
using Npgsql;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;
using System.Threading.Tasks;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public abstract class WalletSystemTestsBase : IClassFixture<GrpcTestFixture<Startup>>, IClassFixture<PostgresDatabaseFixture>, IDisposable
{
    protected readonly string endpoint = "http://my-endpoint:80/";
    protected readonly GrpcTestFixture<Startup> _grpcFixture;
    protected readonly PostgresDatabaseFixture _dbFixture;
    protected readonly JwtGenerator _tokenGenerator;
    private readonly IDisposable _logger;

    protected IHDAlgorithm Algorithm => _grpcFixture.GetRequiredService<IHDAlgorithm>();

    public WalletSystemTestsBase(GrpcTestFixture<Startup> grpcFixture, PostgresDatabaseFixture dbFixture, ITestOutputHelper outputHelper, RegistryFixture? registry)
    {
        _grpcFixture = grpcFixture;
        _dbFixture = dbFixture;
        _logger = grpcFixture.GetTestLogger(outputHelper);
        _tokenGenerator = new JwtGenerator();

        var config = new Dictionary<string, string?>()
        {
            {"ConnectionStrings:Database", dbFixture.ConnectionString},
            {"ServiceOptions:EndpointAddress", endpoint},
            {"VerifySlicesWorkerOptions:SleepTime", "00:00:02"}
        };

        if (registry is not null)
            config.Add($"RegistryUrls:{registry.Name}", registry.RegistryUrl);

        grpcFixture.ConfigureHostConfiguration(config);
    }

    public void Dispose()
    {
        _logger.Dispose();
    }

    protected async Task<WalletSection> CreateWalletSection(string owner)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var walletRepository = new WalletRepository(connection);
            var wallet = new Wallet(Guid.NewGuid(), owner, Algorithm.GenerateNewPrivateKey());
            await walletRepository.Create(wallet);

            var section = new WalletSection(Guid.NewGuid(), wallet.Id, 1, wallet.PrivateKey.Derive(1).Neuter());
            await walletRepository.CreateSection(section);

            return section;
        }
    }

    protected async Task<RegistryModel> CreateRegistry(string name)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var registryRepository = new RegistryRepository(connection);
            var registry = new RegistryModel(Guid.NewGuid(), name);
            await registryRepository.InsertRegistry(registry);

            return registry;
        }
    }

    protected async Task<Certificate> CreateCertificate(Guid id, Guid registryId)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var certificateRepository = new CertificateRepository(connection);
            var attributes = new List<CertificateAttribute>
            {
                new ("AssetId", "571234567890123456"),
                new ("TechCode", "T070000"),
                new ("FuelCode", "F00000000")
            };
            var cert = new Certificate(id, registryId, DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1), "DK1", GranularCertificateType.Production, attributes);
            await certificateRepository.InsertCertificate(cert);

            return cert;
        }
    }

    protected async Task<ReceivedSlice> CreateReceivedSlice(WalletSection walletSection, string registryName, Guid certificateId, long quantity, byte[] randomR)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var certificateRepository = new CertificateRepository(connection);
            var receivedSlice = new ReceivedSlice(Guid.NewGuid(), walletSection.Id, walletSection.WalletPosition,
                registryName, certificateId, quantity, randomR);

            await certificateRepository.InsertReceivedSlice(receivedSlice);
            return receivedSlice;
        }
    }
}
