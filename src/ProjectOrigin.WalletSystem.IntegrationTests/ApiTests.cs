using AutoFixture;
using Dapper;
using Npgsql;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using ProjectOrigin.WalletSystem.Server.Database.Mapping;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;
using GranularCertificateType = ProjectOrigin.WalletSystem.Server.Models.GranularCertificateType;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

[UsesVerify]
public class ApiTests : WalletSystemTestsBase, IClassFixture<InMemoryFixture>
{
    public ApiTests(
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
        SqlMapper.AddTypeHandler<IHDPrivateKey>(new HDPrivateKeyTypeHandler(Algorithm));
        SqlMapper.AddTypeHandler<IHDPublicKey>(new HDPublicKeyTypeHandler(Algorithm));
    }

    [Fact]
    public async Task open_api_specification_not_changed()
    {
        var httpClient = _grpcFixture.CreateHttpClient();
        var specificationResponse = await httpClient.GetAsync("swagger/v1/swagger.json");
        var specification = await specificationResponse.Content.ReadAsStringAsync();
        await Verifier.Verify(specification);
    }

    [Fact]
    public async Task can_query_certificates()
    {
        //Arrange
        var owner = _fixture.Create<string>();
        var someOwnerName = _fixture.Create<string>();
        var httpClient = CreateAuthenticatedHttpClient(owner, someOwnerName);

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

            var walletEndpoint = await walletRepository.CreateWalletEndpoint(wallet.Id);

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
                StartDate = DateTimeOffset.Parse("2023-01-01T12:00Z"),
                EndDate = DateTimeOffset.Parse("2023-01-01T13:00Z"),
                GridArea = "DK1",
                CertificateType = GranularCertificateType.Production,
                Attributes = attributes
            };
            var certificate2 = new Certificate
            {
                Id = Guid.NewGuid(),
                RegistryName = regName,
                StartDate = DateTimeOffset.Parse("2023-01-01T13:00Z"),
                EndDate = DateTimeOffset.Parse("2023-01-01T14:00Z"),
                GridArea = "DK1",
                CertificateType = GranularCertificateType.Production,
                Attributes = attributes
            };
            await certificateRepository.InsertCertificate(certificate1);
            await certificateRepository.InsertCertificate(certificate2);

            var slice1 = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = walletEndpoint.Id,
                WalletEndpointPosition = 1,
                RegistryName = regName,
                CertificateId = certificate1.Id,
                Quantity = 42,
                RandomR = _fixture.Create<byte[]>(),
                State = WalletSliceState.Available
            };
            var slice2 = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = walletEndpoint.Id,
                WalletEndpointPosition = 1,
                RegistryName = regName,
                CertificateId = certificate1.Id,
                Quantity = 43,
                RandomR = _fixture.Create<byte[]>(),
                State = WalletSliceState.Available
            };
            var slice3 = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = walletEndpoint.Id,
                WalletEndpointPosition = 1,
                RegistryName = regName,
                CertificateId = certificate2.Id,
                Quantity = 44,
                RandomR = _fixture.Create<byte[]>(),
                State = WalletSliceState.Available
            };
            await certificateRepository.InsertWalletSlice(slice1);
            await certificateRepository.InsertWalletSlice(slice2);
            await certificateRepository.InsertWalletSlice(slice3);
        }

        //Act
        var res = await httpClient.GetAsync("api/certificates");

        //Assert
        await Verifier.VerifyJson(res.Content.ReadAsStringAsync()); //TODO: Registry not set
    }
}
