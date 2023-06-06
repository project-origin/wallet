using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
using System;
using ProjectOrigin.WalletSystem.Server.HDWallet;
using Npgsql;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;
using System.Threading.Tasks;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public abstract class WalletSystemTestsBase : IClassFixture<GrpcTestFixture<Startup>>, IClassFixture<PostgresDatabaseFixture>, IDisposable
{
    protected readonly string endpoint = "http://my-endpoint:80/";
    protected readonly GrpcTestFixture<Startup> _grpcFixture;
    protected readonly PostgresDatabaseFixture _dbFixture;
    protected readonly JwtGenerator _tokenGenerator;
    private readonly IDisposable _logger;

    protected IHDAlgorithm Algorithm => _grpcFixture.GetRequiredService<IHDAlgorithm>();

    public WalletSystemTestsBase(GrpcTestFixture<Startup> grpcFixture, PostgresDatabaseFixture dbFixture, ITestOutputHelper outputHelper)
    {
        _grpcFixture = grpcFixture;
        _dbFixture = dbFixture;
        _logger = grpcFixture.GetTestLogger(outputHelper);
        _tokenGenerator = new JwtGenerator();

        grpcFixture.ConfigureHostConfiguration(new Dictionary<string, string?>()
         {
             {"ConnectionStrings:Database", dbFixture.ConnectionString},
             {"ServiceOptions:EndpointAddress", endpoint},
             {"VerifySlicesWorkerOptions:SleepTime", "00:00:02"}
         });
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

            var section = new WalletSection(Guid.NewGuid(), wallet.Id, 1, wallet.PrivateKey.Derive(1).PublicKey);
            await walletRepository.CreateSection(section);

            return section;
        }
    }

    protected async Task<Registry> CreateRegistry(string name)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var registryRepository = new RegistryRepository(connection);
            var registry = new Registry(Guid.NewGuid(), name);
            await registryRepository.InsertRegistry(registry);

            return registry;
        }
    }

    protected async Task<Certificate> CreateCertificate(Guid id, Guid registryId)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var certificateRepository = new CertificateRepository(connection);
            var cert = new Certificate(id, registryId);
            await certificateRepository.InsertCertificate(cert);

            return cert;
        }
    }

    protected async Task<ReceivedSlice> CreateReceivedSlice(WalletSection walletSection, string registryName, Guid certificateId, long quantity)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var certificateRepository = new CertificateRepository(connection);
            var receivedSlice = new ReceivedSlice(Guid.NewGuid(), walletSection.Id, walletSection.WalletPosition,
                registryName, certificateId, quantity, new byte[] { 0x01, 0x02, 0x03, 0x04 });

            await certificateRepository.InsertReceivedSlice(receivedSlice);
            return receivedSlice;
        }
    }
}
