using AutoFixture;
using Dapper;
using Npgsql;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using ProjectOrigin.WalletSystem.Server.Database.Mapping;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;
using System;
using System.Collections.Generic;
using FluentAssertions;
using Grpc.Core;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.V1;
using Xunit;
using WalletService = ProjectOrigin.WalletSystem.V1.WalletService;
using Xunit.Abstractions;
using GranularCertificateType = ProjectOrigin.WalletSystem.Server.Models.GranularCertificateType;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.WalletSystem.IntegrationTests
{
    public class QueryCertificatesTest : WalletSystemTestsBase, IClassFixture<InMemoryFixture>
    {
        private Fixture _fixture;

        public QueryCertificatesTest(
            GrpcTestFixture<Startup> grpcFixture,
            PostgresDatabaseFixture dbFixture,
            InMemoryFixture inMemoryFixture,
            ITestOutputHelper outputHelper)
            : base(
                  grpcFixture,
                  dbFixture,
                  inMemoryFixture,
                  outputHelper,
                  null)
        {
            _fixture = new Fixture();

            SqlMapper.AddTypeHandler<IHDPrivateKey>(new HDPrivateKeyTypeHandler(Algorithm));
            SqlMapper.AddTypeHandler<IHDPublicKey>(new HDPublicKeyTypeHandler(Algorithm));
        }

        [Fact]
        public async void QueryCertificates()
        {
            //Arrange
            var owner = _fixture.Create<string>();
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

                var endpoint = await walletRepository.CreateReceiveEndpoint(wallet.Id);
                var notOwnedendpoint = await walletRepository.CreateReceiveEndpoint(notOwnedWallet.Id);

                var regName = _fixture.Create<string>();
                var certificateRepository = new CertificateRepository(connection);

                var attributes = new List<CertificateAttribute>
                {
                    new(){ Key="AssetId", Value="571234567890123456"},
                    new(){ Key="TechCode", Value="T070000"},
                    new(){ Key="FuelCode", Value="F00000000"},
                };

                var certificate1 = new Certificate
                {
                    Id = Guid.NewGuid(),
                    Registry = regName,
                    StartDate = DateTimeOffset.Now,
                    EndDate = DateTimeOffset.Now.AddDays(1),
                    GridArea = "DK1",
                    CertificateType = GranularCertificateType.Production,
                    Attributes = attributes
                };
                var certificate2 = new Certificate
                {
                    Id = Guid.NewGuid(),
                    Registry = regName,
                    StartDate = DateTimeOffset.Now,
                    EndDate = DateTimeOffset.Now.AddDays(1),
                    GridArea = "DK1",
                    CertificateType = GranularCertificateType.Production,
                    Attributes = attributes
                };
                var notOwnedCertificate = new Certificate
                {
                    Id = Guid.NewGuid(),
                    Registry = regName,
                    StartDate = DateTimeOffset.Now,
                    EndDate = DateTimeOffset.Now.AddDays(1),
                    GridArea = "DK1",
                    CertificateType = GranularCertificateType.Production,
                    Attributes = attributes
                };
                await certificateRepository.InsertCertificate(certificate1);
                await certificateRepository.InsertCertificate(certificate2);
                await certificateRepository.InsertCertificate(notOwnedCertificate);

                var slice1 = new Slice
                {
                    Id = Guid.NewGuid(),
                    DepositEndpointId = endpoint.Id,
                    DepositEndpointPosition = 1,
                    Registry = regName,
                    CertificateId = certificate1.Id,
                    Quantity = quantity1,
                    RandomR = _fixture.Create<byte[]>(),
                    SliceState = SliceState.Available
                };
                var slice2 = new Slice
                {
                    Id = Guid.NewGuid(),
                    DepositEndpointId = endpoint.Id,
                    DepositEndpointPosition = 1,
                    Registry = regName,
                    CertificateId = certificate1.Id,
                    Quantity = quantity2,
                    RandomR = _fixture.Create<byte[]>(),
                    SliceState = SliceState.Available
                };
                var slice3 = new Slice
                {
                    Id = Guid.NewGuid(),
                    DepositEndpointId = endpoint.Id,
                    DepositEndpointPosition = 1,
                    Registry = regName,
                    CertificateId = certificate2.Id,
                    Quantity = quantity3,
                    RandomR = _fixture.Create<byte[]>(),
                    SliceState = SliceState.Available
                };
                var notOwnedSlice = new Slice
                {
                    Id = Guid.NewGuid(),
                    DepositEndpointId = notOwnedendpoint.Id,
                    DepositEndpointPosition = 1,
                    Registry = regName,
                    CertificateId = notOwnedCertificate.Id,
                    Quantity = _fixture.Create<long>(),
                    RandomR = _fixture.Create<byte[]>(),
                    SliceState = SliceState.Available
                };
                await certificateRepository.InsertSlice(slice1);
                await certificateRepository.InsertSlice(slice2);
                await certificateRepository.InsertSlice(slice3);
                await certificateRepository.InsertSlice(notOwnedSlice);
            }

            var someOwnerName = _fixture.Create<string>();
            var token = _tokenGenerator.GenerateToken(owner, someOwnerName);
            var headers = new Metadata();
            headers.Add("Authorization", $"Bearer {token}");

            var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);

            //Act
            var result = await client.QueryGranularCertificatesAsync(new QueryRequest(), headers);

            //Assert
            result.GranularCertificates.Should().HaveCount(2);
            result.GranularCertificates.Should().Contain(x => x.Quantity == quantity1 + quantity2);
            result.GranularCertificates.Should().Contain(x => x.Quantity == quantity3);
        }

        [Fact]
        public async void QueryGranularCertificates_WhenNoCertificatesInWallet_ExpectZeroCertificates()
        {
            var owner = _fixture.Create<string>();

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

                var endpoint = await walletRepository.CreateReceiveEndpoint(wallet.Id);
            }

            var someOwnerName = _fixture.Create<string>();
            var token = _tokenGenerator.GenerateToken(owner, someOwnerName);
            var headers = new Metadata();
            headers.Add("Authorization", $"Bearer {token}");

            var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);

            var result = await client.QueryGranularCertificatesAsync(new QueryRequest(), headers);

            result.GranularCertificates.Should().BeEmpty();
        }

        [Fact]
        public async void QueryGranularCertificates_WhenInvalidCertificateTypeInDatabase_ExpectException()
        {
            var owner = _fixture.Create<string>();

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

                var endpoint = await walletRepository.CreateReceiveEndpoint(wallet.Id);

                var regName = _fixture.Create<string>();

                var certId = Guid.NewGuid();
                var registryId = Guid.NewGuid();

                await connection.ExecuteAsync(@"INSERT INTO Registries(Id, Name) VALUES (@registryId, @regName)", new { registryId, regName });

                await connection.ExecuteAsync(@"INSERT INTO Certificates(Id, RegistryId, StartDate, EndDate, GridArea, CertificateType) VALUES (@id, @registryId, @startDate, @endDate, @gridArea, @certificateType)",
                    new { id = certId, registryId, startDate = DateTimeOffset.Now.ToUtcTime(), endDate = DateTimeOffset.Now.AddDays(1).ToUtcTime(), gridArea = "SomeGridArea", certificateType = 0 });

                var slice1 = new Slice
                {
                    Id = Guid.NewGuid(),
                    DepositEndpointId = endpoint.Id,
                    DepositEndpointPosition = 1,
                    Registry = regName,
                    CertificateId = certId,
                    Quantity = 42,
                    RandomR = _fixture.Create<byte[]>(),
                    SliceState = SliceState.Available
                };
                var certificateRepository = new CertificateRepository(connection);
                await certificateRepository.InsertSlice(slice1);
            }

            var someOwnerName = _fixture.Create<string>();
            var token = _tokenGenerator.GenerateToken(owner, someOwnerName);
            var headers = new Metadata();
            headers.Add("Authorization", $"Bearer {token}");

            var client = new WalletService.WalletServiceClient(_grpcFixture.Channel);

            var act = async () => await client.QueryGranularCertificatesAsync(new QueryRequest(), headers);

            await act.Should().ThrowAsync<RpcException>();
        }
    }
}
