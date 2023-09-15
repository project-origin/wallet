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
using System.Linq;
using MassTransit;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public abstract class WalletSystemTestsBase : IClassFixture<GrpcTestFixture<Startup>>, IClassFixture<PostgresDatabaseFixture>, IDisposable
{
    protected readonly string endpoint = "http://localhost/";
    private readonly IMessageBrokerFixture _messageBrokerFixture;
    protected readonly GrpcTestFixture<Startup> _grpcFixture;
    protected readonly PostgresDatabaseFixture _dbFixture;
    protected readonly JwtGenerator _tokenGenerator;
    private readonly IDisposable _logger;

    protected IHDAlgorithm Algorithm => _grpcFixture.GetRequiredService<IHDAlgorithm>();

    public WalletSystemTestsBase(
        GrpcTestFixture<Startup> grpcFixture,
        PostgresDatabaseFixture dbFixture,
        IMessageBrokerFixture messageBrokerFixture,
        ITestOutputHelper outputHelper,
        RegistryFixture? registry)
    {
        _messageBrokerFixture = messageBrokerFixture;
        _grpcFixture = grpcFixture;
        _dbFixture = dbFixture;
        _logger = grpcFixture.GetTestLogger(outputHelper);
        _tokenGenerator = new JwtGenerator();

        var config = new Dictionary<string, string?>()
        {
            {"ConnectionStrings:Database", dbFixture.ConnectionString},
            {"ServiceOptions:EndpointAddress", endpoint},
            {"VerifySlicesWorkerOptions:SleepTime", "00:00:02"},
        };

        config = config.Concat(_messageBrokerFixture.Configuration).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

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
            var wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                Owner = owner,
                PrivateKey = Algorithm.GenerateNewPrivateKey()
            };
            await walletRepository.Create(wallet);

            return await walletRepository.CreateDepositEndpoint(wallet.Id, string.Empty);
        }
    }

    protected async Task<Certificate> CreateCertificate(Guid id, string registryName)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var certificateRepository = new CertificateRepository(connection);
            var attributes = new List<CertificateAttribute>
            {
                new () {Key="AssetId", Value= "571234567890123456"},
                new () {Key="TechCode", Value= "T070000"},
                new () {Key="FuelCode", Value= "F00000000"}
            };
            var cert = new Certificate
            {
                Id = id,
                Registry = registryName,
                StartDate = DateTimeOffset.Now,
                EndDate = DateTimeOffset.Now.AddDays(1),
                GridArea = "DK1",
                CertificateType = GranularCertificateType.Production,
                Attributes = attributes
            };
            await certificateRepository.InsertCertificate(cert);

            return cert;
        }
    }

    protected async Task InsertSlice(DepositEndpoint depositEndpoint, int position, Electricity.V1.IssuedEvent issuedEvent, SecretCommitmentInfo commitment)
    {
        using var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        var certificateRepository = new CertificateRepository(connection);

        var certificate = await certificateRepository.GetCertificate(issuedEvent.CertificateId.Registry, Guid.Parse(issuedEvent.CertificateId.StreamId.Value));

        if (certificate is null)
        {
            certificate = new Certificate
            {
                Id = Guid.Parse(issuedEvent.CertificateId.StreamId.Value),
                Registry = issuedEvent.CertificateId.Registry,
                StartDate = issuedEvent.Period.Start.ToDateTimeOffset(),
                EndDate = issuedEvent.Period.End.ToDateTimeOffset(),
                GridArea = issuedEvent.GridArea,
                CertificateType = (GranularCertificateType)issuedEvent.Type
            };

            await certificateRepository.InsertCertificate(certificate);
        }

        var receivedSlice = new Slice
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = depositEndpoint.Id,
            DepositEndpointPosition = position,
            Registry = certificate.Registry,
            CertificateId = certificate.Id,
            Quantity = commitment.Message,
            RandomR = commitment.BlindingValue.ToArray(),
            SliceState = SliceState.Available
        };

        await certificateRepository.InsertSlice(receivedSlice);
    }
}
