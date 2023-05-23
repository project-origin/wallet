using AutoFixture;
using Dapper;
using Npgsql;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using ProjectOrigin.WalletSystem.Server.Database.Mapping;
using ProjectOrigin.WalletSystem.Server.HDWallet;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;
using System;
using FluentAssertions;
using Grpc.Core;
using ProjectOrigin.WalletSystem.V1;
using Xunit;
using WalletService = ProjectOrigin.WalletSystem.V1.WalletService;

namespace ProjectOrigin.WalletSystem.IntegrationTests
{
    public class QueryCertificatesTest : GrpcTestsBase
    {
        public QueryCertificatesTest(GrpcTestFixture<Startup> grpcFixture, PostgresDatabaseFixture dbFixture) : base(grpcFixture, dbFixture)
        {
        }

        [Fact]
        public async void QueryCertificates()
        {
            //Arrange
            var owner = "SomeOwner";

            SqlMapper.AddTypeHandler<IHDPrivateKey>(new HDPrivateKeyTypeHandler(_algorithm));
            SqlMapper.AddTypeHandler<IHDPublicKey>(new HDPublicKeyTypeHandler(_algorithm));

            var quantity1 = _fixture.Create<long>();
            var quantity2 = _fixture.Create<long>();
            var quantity3 = _fixture.Create<long>();

            using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
            {
                var walletRepository = new WalletRepository(connection);
                var wallet = new OwnerWallet(Guid.NewGuid(), owner, _algorithm.GenerateNewPrivateKey());
                var notOwnedWallet = new OwnerWallet(Guid.NewGuid(), "SomeOtherOwner", _algorithm.GenerateNewPrivateKey());
                await walletRepository.Create(wallet);
                await walletRepository.Create(notOwnedWallet);

                var section = new WalletSection(Guid.NewGuid(), wallet.Id, 1, wallet.PrivateKey.Derive(1).PublicKey);
                var notOwnedSection = new WalletSection(Guid.NewGuid(), notOwnedWallet.Id, 1, notOwnedWallet.PrivateKey.Derive(1).PublicKey);
                await walletRepository.CreateSection(section);
                await walletRepository.CreateSection(notOwnedSection);

                var registry = new Registry(Guid.NewGuid(), "SomeRegistry");
                var certificateRepository = new CertificateRepository(connection);
                await certificateRepository.InsertRegistry(registry);

                var certificate1 = new Certificate(Guid.NewGuid(), registry.Id, CertificateState.Inserted);
                var certificate2 = new Certificate(Guid.NewGuid(), registry.Id, CertificateState.Inserted);
                var notOwnedCertificate = new Certificate(Guid.NewGuid(), registry.Id, CertificateState.Inserted);
                await certificateRepository.InsertCertificate(certificate1);
                await certificateRepository.InsertCertificate(certificate2);
                await certificateRepository.InsertCertificate(notOwnedCertificate);

                var slice1 = new Slice(Guid.NewGuid(), section.Id, 1, registry.Id, certificate1.Id, quantity1, _fixture.Create<byte[]>(), SliceState.Unverified);
                var slice2 = new Slice(Guid.NewGuid(), section.Id, 1, registry.Id, certificate1.Id, quantity2, _fixture.Create<byte[]>(), SliceState.Unverified);
                var slice3 = new Slice(Guid.NewGuid(), section.Id, 1, registry.Id, certificate2.Id, quantity3, _fixture.Create<byte[]>(), SliceState.Unverified);
                var notOwnedSlice = new Slice(Guid.NewGuid(), notOwnedSection.Id, 1, registry.Id, notOwnedCertificate.Id, _fixture.Create<long>(), _fixture.Create<byte[]>(), SliceState.Unverified);
                await certificateRepository.InsertSlice(slice1);
                await certificateRepository.InsertSlice(slice2);
                await certificateRepository.InsertSlice(slice3);
                await certificateRepository.InsertSlice(notOwnedSlice);
            }

            var token = _tokenGenerator.GenerateToken(owner, "John Doe");
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
    }
}
