using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using ProjectOrigin.PedersenCommitment;
using FluentAssertions;
using System;

namespace ProjectOrigin.WalletSystem.IntegrationTests.FlowTests;

public class ReceiveTests : AbstractFlowTests
{
    public ReceiveTests(
            GrpcTestFixture<Startup> grpcFixture,
            PostgresDatabaseFixture dbFixture,
            InMemoryFixture inMemoryFixture,
            RegistryFixture registryFixture,
            ITestOutputHelper outputHelper)
            : base(
                  grpcFixture,
                  dbFixture,
                  inMemoryFixture,
                  outputHelper,
                  registryFixture)
    {
    }

    [Fact]
    public async Task IssueCertWithAndWithoutAttributes_Query_Success()
    {
        var client = new V1.WalletService.WalletServiceClient(_grpcFixture.Channel);
        var position = 1;

        var (owner, header) = GenerateUserHeader();
        var endpoint = await client.CreateWalletDepositEndpointAsync(new V1.CreateWalletDepositEndpointRequest(), header);

        var prodCertId = await IssueCertificateToEndpoint(
            endpoint.WalletDepositEndpoint,
            Electricity.V1.GranularCertificateType.Production,
            new SecretCommitmentInfo(250),
            position++,
            new(){
                ("TechCode", "T010101", null),
                ("FuelCode", "F010101", null),
            });

        var conCertId = await IssueCertificateToEndpoint(
            endpoint.WalletDepositEndpoint,
            Electricity.V1.GranularCertificateType.Consumption,
            new SecretCommitmentInfo(150),
            position++);

        var certificates = await Timeout(async () =>
        {
            var result = await client.QueryGranularCertificatesAsync(new V1.QueryRequest(), header);
            result.GranularCertificates.Should().HaveCount(2);
            return result.GranularCertificates;
        }, TimeSpan.FromMinutes(1));

        var gc1 = certificates.Should().Contain(x => x.FederatedId.StreamId.Value == prodCertId.StreamId.Value).Which;
        gc1.Type.Should().Be(V1.GranularCertificateType.Production);
        gc1.Quantity.Should().Be(250);
        gc1.Attributes.Should().HaveCount(2);

        var gc2 = certificates.Should().Contain(x => x.FederatedId.StreamId.Value == conCertId.StreamId.Value).Which;
        gc2.Type.Should().Be(V1.GranularCertificateType.Consumption);
        gc2.Quantity.Should().Be(150);
        gc2.Attributes.Should().HaveCount(0);
    }


    [Fact]
    public async Task IssueCertWithAttributes_Query_Success()
    {
        var client = new V1.WalletService.WalletServiceClient(_grpcFixture.Channel);
        var position = 1;

        var (owner, header) = GenerateUserHeader();
        var endpoint = await client.CreateWalletDepositEndpointAsync(new V1.CreateWalletDepositEndpointRequest(), header);

        var certificateId = await IssueCertificateToEndpoint(
            endpoint.WalletDepositEndpoint,
            Electricity.V1.GranularCertificateType.Production,
            new SecretCommitmentInfo(250),
            position++,
            new(){
                ("TechCode", "T010101", null),
                ("FuelCode", "F010101", null),
                ("AssetId", "1264541", new byte[] { 0x01, 0x02, 0x03, 0x04 }),
            });

        var certificates = await Timeout(async () =>
        {
            var result = await client.QueryGranularCertificatesAsync(new V1.QueryRequest(), header);
            result.GranularCertificates.Should().HaveCount(1);
            return result.GranularCertificates;
        }, TimeSpan.FromMinutes(1));

        var gc = certificates.Should().Contain(x => x.FederatedId.StreamId.Value == certificateId.StreamId.Value).Which;
        gc.Type.Should().Be(V1.GranularCertificateType.Production);
        gc.Quantity.Should().Be(250);
        gc.Attributes.Should().HaveCount(3)
            .And.Contain(x => x.Key == "TechCode" && x.Value == "T010101")
            .And.Contain(x => x.Key == "FuelCode" && x.Value == "F010101")
            .And.Contain(x => x.Key == "AssetId" && x.Value == "1264541");
    }
}
