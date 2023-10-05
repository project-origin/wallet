using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Npgsql;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Database.Postgres;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;

namespace ProjectOrigin.WalletSystem.IntegrationTests.TestExtensions;

public static class PostgresFixtureExtensions
{
    public static IUnitOfWork CreateUnitOfWork(this PostgresDatabaseFixture _dbFixture)
    {
        var options = Options.Create(new PostgresOptions { ConnectionString = _dbFixture.ConnectionString });
        var dbFactory = new PostgresConnectionFactory(options);
        return new UnitOfWork(dbFactory);
    }

    public static async Task<Wallet> CreateWallet(this PostgresDatabaseFixture _dbFixture, string owner)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var walletRepository = new WalletRepository(connection);
            var wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                Owner = owner,
                PrivateKey = new Secp256k1Algorithm().GenerateNewPrivateKey()
            };
            await walletRepository.Create(wallet);

            return wallet;
        }
    }

    public static async Task<DepositEndpoint> CreateDepositEndpoint(this PostgresDatabaseFixture _dbFixture, Wallet wallet)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var walletRepository = new WalletRepository(connection);
            return await walletRepository.CreateDepositEndpoint(wallet.Id, string.Empty);
        }
    }

    public static async Task<DepositEndpoint> CreateWalletDepositEndpoint(this PostgresDatabaseFixture _dbFixture, string owner)
    {
        var wallet = await CreateWallet(_dbFixture, owner);
        return await CreateDepositEndpoint(_dbFixture, wallet);
    }

    public static async Task<Certificate> CreateCertificate(this PostgresDatabaseFixture _dbFixture, Guid id, string registryName, GranularCertificateType type)
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
                CertificateType = type,
                Attributes = attributes
            };
            await certificateRepository.InsertCertificate(cert);

            return cert;
        }
    }

    public static async Task<Slice> CreateSlice(this PostgresDatabaseFixture _dbFixture, DepositEndpoint depositEndpoint, Certificate certificate, SecretCommitmentInfo secretCommitmentInfo)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var certificateRepository = new CertificateRepository(connection);
            var walletRepository = new WalletRepository(connection);
            var slice = new Slice
            {
                Id = Guid.NewGuid(),
                DepositEndpointId = depositEndpoint.Id,
                DepositEndpointPosition = await walletRepository.GetNextNumberForId(depositEndpoint.Id),
                Registry = certificate.Registry,
                CertificateId = certificate.Id,
                Quantity = secretCommitmentInfo.Message,
                RandomR = secretCommitmentInfo.BlindingValue.ToArray(),
                SliceState = SliceState.Available
            };

            await certificateRepository.InsertSlice(slice);

            return slice;
        }
    }

    public static async Task InsertSlice(this PostgresDatabaseFixture _dbFixture, DepositEndpoint depositEndpoint, int position, Electricity.V1.IssuedEvent issuedEvent, SecretCommitmentInfo commitment)
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
