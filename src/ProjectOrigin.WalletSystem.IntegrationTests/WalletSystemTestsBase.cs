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
using ProjectOrigin.PedersenCommitment;
using Dapper;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public abstract class WalletSystemTestsBase : IClassFixture<GrpcTestFixture<Startup>>, IClassFixture<PostgresDatabaseFixture>, IDisposable
{
    protected readonly string endpoint = "http://localhost/";
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

    protected async Task<DepositEndpoint> CreateWalletDepositEndpoint(string owner)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var walletRepository = new WalletRepository(connection);
            var wallet = new Wallet(Guid.NewGuid(), owner, Algorithm.GenerateNewPrivateKey());
            await walletRepository.Create(wallet);

            return await walletRepository.CreateDepositEndpoint(wallet.Id, string.Empty);
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

    protected async Task<ReceivedSlice> CreateReceivedSlice(DepositEndpoint depositEndpoint, string registryName, Guid certificateId, long quantity, byte[] randomR)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var certificateRepository = new CertificateRepository(connection);
            var receivedSlice = new ReceivedSlice(Guid.NewGuid(), depositEndpoint.Id, depositEndpoint.WalletPosition!.Value,
                registryName, certificateId, quantity, randomR);

            await certificateRepository.InsertReceivedSlice(receivedSlice);
            return receivedSlice;
        }
    }

    protected async Task InsertSlice(DepositEndpoint depositEndpoint, int position, Electricity.V1.IssuedEvent issuedEvent, SecretCommitmentInfo commitment)
    {
        using var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        var certificateRepository = new CertificateRepository(connection);

        RegistryModel registry = await GetOrInsertRegistry(connection, issuedEvent.CertificateId.Registry);
        var certificate = await certificateRepository.GetCertificate(registry.Id, Guid.Parse(issuedEvent.CertificateId.StreamId.Value));

        if (certificate is null)
        {
            certificate = new Certificate(
                Guid.Parse(issuedEvent.CertificateId.StreamId.Value),
                registry.Id,
                issuedEvent.Period.Start.ToDateTimeOffset(),
                issuedEvent.Period.End.ToDateTimeOffset(),
                issuedEvent.GridArea,
                (GranularCertificateType)issuedEvent.Type,
                new List<CertificateAttribute>());

            await certificateRepository.InsertCertificate(certificate);
        }

        var receivedSlice = new Slice(Guid.NewGuid(), depositEndpoint.Id, position,
            certificate.RegistryId, certificate.Id, commitment.Message, commitment.BlindingValue.ToArray(), SliceState.Available);

        await certificateRepository.InsertSlice(receivedSlice);
    }

    protected static async Task<RegistryModel> GetOrInsertRegistry(NpgsqlConnection connection, string registryName)
    {
        var registryRepository = new RegistryRepository(connection);
        var registry = await registryRepository.GetRegistryFromName(registryName);
        if (registry is null)
        {
            registry = new RegistryModel(Guid.NewGuid(), registryName);
            await registryRepository.InsertRegistry(registry);
        }

        return registry;
    }
}
