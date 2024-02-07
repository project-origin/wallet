using AutoFixture;
using Dapper;
using FluentAssertions;
using Grpc.Core;
using Npgsql;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using ProjectOrigin.WalletSystem.Server.Database.Mapping;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;
using ProjectOrigin.WalletSystem.V1;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
using GranularCertificateType = ProjectOrigin.WalletSystem.Server.Models.GranularCertificateType;
using WalletService = ProjectOrigin.WalletSystem.V1.WalletService;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class QueryCertificatesTest : WalletSystemTestsBase, IClassFixture<InMemoryFixture>
{
    public QueryCertificatesTest(
        TestServerFixture<Startup> serverFixture,
        PostgresDatabaseFixture dbFixture,
        InMemoryFixture inMemoryFixture,
        JwtTokenIssuerFixture jwtTokenIssuerFixture,
        ITestOutputHelper outputHelper)
        : base(
              serverFixture,
              dbFixture,
              inMemoryFixture,
              jwtTokenIssuerFixture,
              outputHelper,
              null)
    {
    }

    [Fact]
    public async void QueryCertificates()
    {
        //Arrange
        var (owner, header) = GenerateUserHeader();
        var someOtherOwner = _fixture.Create<string>();

        var quantity1 = _fixture.Create<long>();
        var quantity2 = _fixture.Create<long>();
        var quantity3 = _fixture.Create<long>();

        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var walletRepository = new WalletRepository(connection);
            var wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                Owner = owner,
                PrivateKey = Algorithm.GenerateNewPrivateKey()
            };
            var notOwnedWallet = new Wallet
            {
                Id = Guid.NewGuid(),
                Owner = someOtherOwner,
                PrivateKey = Algorithm.GenerateNewPrivateKey()
            };
            await walletRepository.Create(wallet);
            await walletRepository.Create(notOwnedWallet);

            var endpoint = await walletRepository.CreateWalletEndpoint(wallet.Id);
            var notOwnedendpoint = await walletRepository.CreateWalletEndpoint(notOwnedWallet.Id);

            var regName = _fixture.Create<string>();
            var certificateRepository = new CertificateRepository(connection);

            var attributes = new List<CertificateAttribute>
                {
                    new(){ Key="TechCode", Value="T070000", Type=CertificateAttributeType.ClearText},
                    new(){ Key="FuelCode", Value="F00000000", Type=CertificateAttributeType.ClearText},
                };

            var certificate1 = new Certificate
            {
                Id = Guid.NewGuid(),
                RegistryName = regName,
                StartDate = DateTimeOffset.Now,
                EndDate = DateTimeOffset.Now.AddDays(1),
                GridArea = "DK1",
                CertificateType = GranularCertificateType.Production,
                Attributes = attributes
            };
            var certificate2 = new Certificate
            {
                Id = Guid.NewGuid(),
                RegistryName = regName,
                StartDate = DateTimeOffset.Now,
                EndDate = DateTimeOffset.Now.AddDays(1),
                GridArea = "DK1",
                CertificateType = GranularCertificateType.Production,
                Attributes = attributes
            };
            var notOwnedCertificate = new Certificate
            {
                Id = Guid.NewGuid(),
                RegistryName = regName,
                StartDate = DateTimeOffset.Now,
                EndDate = DateTimeOffset.Now.AddDays(1),
                GridArea = "DK1",
                CertificateType = GranularCertificateType.Production,
                Attributes = attributes
            };
            await certificateRepository.InsertCertificate(certificate1);
            await certificateRepository.InsertCertificate(certificate2);
            await certificateRepository.InsertCertificate(notOwnedCertificate);

            var slice1 = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = endpoint.Id,
                WalletEndpointPosition = 1,
                RegistryName = regName,
                CertificateId = certificate1.Id,
                Quantity = quantity1,
                RandomR = _fixture.Create<byte[]>(),
                State = WalletSliceState.Available
            };
            var slice2 = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = endpoint.Id,
                WalletEndpointPosition = 1,
                RegistryName = regName,
                CertificateId = certificate1.Id,
                Quantity = quantity2,
                RandomR = _fixture.Create<byte[]>(),
                State = WalletSliceState.Available
            };
            var slice3 = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = endpoint.Id,
                WalletEndpointPosition = 1,
                RegistryName = regName,
                CertificateId = certificate2.Id,
                Quantity = quantity3,
                RandomR = _fixture.Create<byte[]>(),
                State = WalletSliceState.Available
            };
            var notOwnedSlice = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = notOwnedendpoint.Id,
                WalletEndpointPosition = 1,
                RegistryName = regName,
                CertificateId = notOwnedCertificate.Id,
                Quantity = _fixture.Create<long>(),
                RandomR = _fixture.Create<byte[]>(),
                State = WalletSliceState.Available
            };
            await certificateRepository.InsertWalletSlice(slice1);
            await certificateRepository.InsertWalletSlice(slice2);
            await certificateRepository.InsertWalletSlice(slice3);
            await certificateRepository.InsertWalletSlice(notOwnedSlice);
        }

        var client = new WalletService.WalletServiceClient(_serverFixture.Channel);

        //Act
        var result = await client.QueryGranularCertificatesAsync(new QueryRequest(), header);

        //Assert
        result.GranularCertificates.Should().HaveCount(2);
        result.GranularCertificates.Should().Contain(x => x.Quantity == quantity1 + quantity2)
            .And.Contain(x => x.Attributes.Count == 2);
        result.GranularCertificates.Should().Contain(x => x.Quantity == quantity3);
    }

    [Fact]
    public async void QueryGranularCertificates_WhenNoCertificatesInWallet_ExpectZeroCertificates()
    {
        var (owner, header) = GenerateUserHeader();

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

            var endpoint = await walletRepository.CreateWalletEndpoint(wallet.Id);
        }

        var client = new WalletService.WalletServiceClient(_serverFixture.Channel);

        var result = await client.QueryGranularCertificatesAsync(new QueryRequest(), header);

        result.GranularCertificates.Should().BeEmpty();
    }

    [Fact]
    public async void QueryGranularCertificates_WhenInvalidCertificateTypeInDatabase_ExpectException()
    {
        var (owner, header) = GenerateUserHeader();

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

            var endpoint = await walletRepository.CreateWalletEndpoint(wallet.Id);

            var regName = _fixture.Create<string>();

            var certId = Guid.NewGuid();

            await connection.ExecuteAsync(@"INSERT INTO Certificates(id, registry_name, start_date, end_date, grid_area, certificate_type) VALUES (@id, @regName, @startDate, @endDate, @gridArea, @certificateType)",
                new { id = certId, regName, startDate = DateTimeOffset.Now.ToUtcTime(), endDate = DateTimeOffset.Now.AddDays(1).ToUtcTime(), gridArea = "SomeGridArea", certificateType = 0 });

            var slice1 = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = endpoint.Id,
                WalletEndpointPosition = 1,
                RegistryName = regName,
                CertificateId = certId,
                Quantity = 42,
                RandomR = _fixture.Create<byte[]>(),
                State = WalletSliceState.Available
            };
            var certificateRepository = new CertificateRepository(connection);
            await certificateRepository.InsertWalletSlice(slice1);
        }

        var client = new WalletService.WalletServiceClient(_serverFixture.Channel);

        var act = async () => await client.QueryGranularCertificatesAsync(new QueryRequest(), header);

        await act.Should().ThrowAsync<RpcException>();
    }
}
