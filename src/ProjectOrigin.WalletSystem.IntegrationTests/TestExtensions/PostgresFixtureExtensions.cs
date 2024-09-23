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

    public static async Task<WalletEndpoint> CreateWalletEndpoint(this PostgresDatabaseFixture _dbFixture, Wallet wallet)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var walletRepository = new WalletRepository(connection);
            return await walletRepository.CreateWalletEndpoint(wallet.Id);
        }
    }

    public static async Task<ExternalEndpoint> CreateExternalEndpoint(this PostgresDatabaseFixture _dbFixture, string owner)
    {
        var key = new Secp256k1Algorithm().GenerateNewPrivateKey();
        var publicKey = key.Derive(0).Neuter();

        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var walletRepository = new WalletRepository(connection);
            return await walletRepository.CreateExternalEndpoint(owner, publicKey, "", "");
        }
    }

    public static async Task<WalletEndpoint> CreateWalletEndpoint(this PostgresDatabaseFixture _dbFixture, string owner)
    {
        var wallet = await CreateWallet(_dbFixture, owner);
        return await CreateWalletEndpoint(_dbFixture, wallet);
    }

    public static async Task<int> GetNextNumberForId(this PostgresDatabaseFixture _dbFixture, Guid id)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var walletRepository = new WalletRepository(connection);

            return await walletRepository.GetNextNumberForId(id);
        }
    }

    public static async Task<WalletEndpoint> GetWalletRemainderEndpoint(this PostgresDatabaseFixture _dbFixture, Guid walletId)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var walletRepository = new WalletRepository(connection);
            return await walletRepository.GetWalletRemainderEndpoint(walletId);
        }
    }

    public static async Task<Certificate> CreateCertificate(
        this PostgresDatabaseFixture _dbFixture,
        Guid id,
        string registryName,
        GranularCertificateType type,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null
        )
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var certificateRepository = new CertificateRepository(connection);
            var attributes = new List<CertificateAttribute>
            {
                new () {Key="TechCode", Value= "T070000", Type=CertificateAttributeType.ClearText},
                new () {Key="FuelCode", Value= "F00000000", Type=CertificateAttributeType.ClearText}
            };

            var cert = new Certificate
            {
                Id = id,
                RegistryName = registryName,
                StartDate = start ?? DateTimeOffset.Now,
                EndDate = end ?? start?.AddDays(1) ?? DateTimeOffset.Now.AddDays(1),
                GridArea = "DK1",
                CertificateType = type,
                Attributes = attributes,
                Withdrawn = false
            };
            await certificateRepository.InsertCertificate(cert);

            return cert;
        }
    }

    public static async Task<WalletSlice> CreateSlice(this PostgresDatabaseFixture _dbFixture, WalletEndpoint walletEndpoint, Certificate certificate, SecretCommitmentInfo secretCommitmentInfo)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var certificateRepository = new CertificateRepository(connection);
            var walletRepository = new WalletRepository(connection);
            var slice = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = walletEndpoint.Id,
                WalletEndpointPosition = await walletRepository.GetNextNumberForId(walletEndpoint.Id),
                RegistryName = certificate.RegistryName,
                CertificateId = certificate.Id,
                Quantity = secretCommitmentInfo.Message,
                RandomR = secretCommitmentInfo.BlindingValue.ToArray(),
                State = WalletSliceState.Available
            };

            await certificateRepository.InsertWalletSlice(slice);

            return slice;
        }
    }

    public static async Task<Claim> CreateClaim(this PostgresDatabaseFixture _dbFixture, WalletSlice prodSlice, WalletSlice consSlice, ClaimState state)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var claimRepository = new ClaimRepository(connection);
            var claim = new Claim
            {
                Id = Guid.NewGuid(),
                ProductionSliceId = prodSlice.Id,
                ConsumptionSliceId = consSlice.Id,
                State = state,
            };

            await claimRepository.InsertClaim(claim);

            return claim;
        }
    }

    public static async Task<TransferredSlice> CreateTransferredSlice(
        this PostgresDatabaseFixture _dbFixture,
        ExternalEndpoint externalEndpoint,
        Certificate certificate,
        SecretCommitmentInfo secretCommitmentInfo)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var walletRepository = new WalletRepository(connection);
            var transferRepository = new TransferRepository(connection);
            var transferredSlice = new TransferredSlice()
            {
                Id = Guid.NewGuid(),
                CertificateId = certificate.Id,
                RegistryName = certificate.RegistryName,
                ExternalEndpointId = externalEndpoint.Id,
                ExternalEndpointPosition = await walletRepository.GetNextNumberForId(externalEndpoint.Id),
                State = TransferredSliceState.Transferred,
                Quantity = secretCommitmentInfo.Message,
                RandomR = secretCommitmentInfo.BlindingValue.ToArray()
            };
            await transferRepository.InsertTransferredSlice(transferredSlice);
            return transferredSlice;
        }
    }

    public static async Task CreateRequestStatus(this PostgresDatabaseFixture _dbFixture, RequestStatus status)
    {
        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var statusRepository = new RequestStatusRepository(connection);

            await statusRepository.InsertRequestStatus(status);
        }
    }

    public static async Task InsertSlice(this PostgresDatabaseFixture _dbFixture, WalletEndpoint endpoint, int position, Electricity.V1.IssuedEvent issuedEvent, SecretCommitmentInfo commitment)
    {
        using var connection = new NpgsqlConnection(_dbFixture.ConnectionString);
        var certificateRepository = new CertificateRepository(connection);

        var certificate = await certificateRepository.GetCertificate(issuedEvent.CertificateId.Registry, Guid.Parse(issuedEvent.CertificateId.StreamId.Value));

        if (certificate is null)
        {
            certificate = new Certificate
            {
                Id = Guid.Parse(issuedEvent.CertificateId.StreamId.Value),
                RegistryName = issuedEvent.CertificateId.Registry,
                StartDate = issuedEvent.Period.Start.ToDateTimeOffset(),
                EndDate = issuedEvent.Period.End.ToDateTimeOffset(),
                GridArea = issuedEvent.GridArea,
                CertificateType = (GranularCertificateType)issuedEvent.Type,
                Withdrawn = false
            };

            await certificateRepository.InsertCertificate(certificate);
        }

        var receivedSlice = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = position,
            RegistryName = certificate.RegistryName,
            CertificateId = certificate.Id,
            Quantity = commitment.Message,
            RandomR = commitment.BlindingValue.ToArray(),
            State = WalletSliceState.Available
        };

        await certificateRepository.InsertWalletSlice(receivedSlice);
    }
}
