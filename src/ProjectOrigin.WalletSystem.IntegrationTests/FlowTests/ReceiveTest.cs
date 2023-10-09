using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using ProjectOrigin.PedersenCommitment;
using FluentAssertions;
using System;

namespace ProjectOrigin.WalletSystem.IntegrationTests.FlowTests;

public class ReceiveTest : AbstractFlowTests
{
    public ReceiveTest(
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
                { "TechCode", "T010101" },
                { "FuelCode", "F010101" },
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
}
