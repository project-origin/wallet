using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using System.Net.Http;
using System.Text;
using System.Net.Http.Headers;
using System;
using AutoFixture;
using System.Collections.Generic;
using ProjectOrigin.WalletSystem.Server.Services.REST.v1;
using System.Text.Json;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class RestFlowTest : AbstractFlowTests
{
    public RestFlowTest(
            TestServerFixture<Startup> serverFixture,
            PostgresDatabaseFixture dbFixture,
            InMemoryFixture inMemoryFixture,
            JwtTokenIssuerFixture jwtTokenIssuerFixture,
            RegistryFixture registryFixture,
            ITestOutputHelper outputHelper)
            : base(
                  serverFixture,
                  dbFixture,
                  inMemoryFixture,
                  jwtTokenIssuerFixture,
                  outputHelper,
                  registryFixture)
    {
    }

    [Fact]
    public async Task Transfer_SingleSlice_LocalWallet()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();

        var client = _serverFixture.CreateHttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtTokenIssuer.GenerateToken(subject, _fixture.Create<string>()));

        // create wallet
        var walletResponse = await client.PostAsync("v1/wallets", ToJsonContent(new { })).ParseJson<CreateWalletResponse>();

        // create wallet endpoint
        var createEndpointResponse = await client.PostAsync($"v1/wallets/{walletResponse.WalletId}/endpoints", ToJsonContent(new { })).ParseJson<CreateWalletEndpointResponse>();

        // issue certificate to registry
        var position = 1;
        var issuedCommitment = new PedersenCommitment.SecretCommitmentInfo(150);
        var issuedCertificateId = await IssueCertificateToEndpoint(
            createEndpointResponse.WalletReference,
            Electricity.V1.GranularCertificateType.Consumption,
            issuedCommitment,
            position);

        // Act
        // send slice to wallet
        await client.PostAsync("v1/slices", ToJsonContent(new ReceiveRequest
        {
            PublicKey = createEndpointResponse.WalletReference.PublicKey.Export().ToArray(),
            Position = (uint)position,
            CertificateId = issuedCertificateId,
            Quantity = issuedCommitment.Message,
            RandomR = issuedCommitment.BlindingValue.ToArray(),
            HashedAttributes = new List<HashedAttribute>()
        })).ParseJson<ReceiveResponse>();

        // Assert
        var certificates = await Timeout(async () =>
        {
            var response = await client.GetAsync("v1/certificates").ParseJson<ResultList<GranularCertificate, PageInfo>>();
            response.Result.Should().HaveCount(1);
            return response.Result;
        }, TimeSpan.FromMinutes(1));
    }

    private static StringContent ToJsonContent(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
