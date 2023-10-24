using AutoFixture;
using Dapper;
using FluentAssertions;
using Npgsql;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using ProjectOrigin.WalletSystem.Server.Database.Mapping;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;
using ProjectOrigin.WalletSystem.Server.Services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using Xunit.Abstractions;
using GranularCertificateType = ProjectOrigin.WalletSystem.Server.Models.GranularCertificateType;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

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
    public async void QueryCertificates()
    {
        //Arrange
        var owner = _fixture.Create<string>();

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
            await walletRepository.Create(wallet);

            var endpoint = await walletRepository.CreateWalletEndpoint(wallet.Id);

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
            await certificateRepository.InsertCertificate(certificate1);
            await certificateRepository.InsertCertificate(certificate2);

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
            await certificateRepository.InsertWalletSlice(slice1);
            await certificateRepository.InsertWalletSlice(slice2);
            await certificateRepository.InsertWalletSlice(slice3);
        }

        var someOwnerName = _fixture.Create<string>();

        var httpClient = CreateAuthenticatedHttpClient(owner, someOwnerName);

        var result = (await
            httpClient.GetFromJsonAsync<ResultModel<ApiGranularCertificate>>("api/certificates"))!;

        //TODO: Registry not set

        //Assert
        result.Result.Should().HaveCount(2);
        result.Result.Should().Contain(x => x.Quantity == quantity1 + quantity2);
            //.And.Contain(x => x.Attributes.Count == 2); //TODO
        result.Result.Should().Contain(x => x.Quantity == quantity3);
    }

    [Fact]
    public async void Swagger()
    {
        var httpClient = _grpcFixture.CreateHttpClient();
        var swaggerResponse = await httpClient.GetAsync("swagger/v1/swagger.json");
        swaggerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var readAsStringAsync = await swaggerResponse.Content.ReadAsStringAsync();
        readAsStringAsync.Should().Be("foo");
    }
}
