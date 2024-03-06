using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Grpc.Core;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using ProjectOrigin.WalletSystem.V1;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class JwtTests : IClassFixture<PostgresDatabaseFixture>, IClassFixture<InMemoryFixture>
{
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly IMessageBrokerFixture _messageBrokerFixture;
    private readonly ITestOutputHelper _outputHelper;

    public JwtTests(
        PostgresDatabaseFixture dbFixture,
        InMemoryFixture messageBrokerFixture,
        ITestOutputHelper outputHelper)
    {
        _dbFixture = dbFixture;
        _messageBrokerFixture = messageBrokerFixture;
        _outputHelper = outputHelper;
    }

    protected readonly string endpoint = "http://localhost/";

    [Fact]
    public async Task JwtVerification_Valid()
    {
        // Arrange
        var jwtTokenIssuerFixture = new JwtTokenIssuerFixture();

        var jwtConfiguration = new Dictionary<string, string?>()
        {
            {"Jwt:Audience", jwtTokenIssuerFixture.Audience},
            {"Jwt:Issuers:0:IssuerName", jwtTokenIssuerFixture.Issuer},
            {"Jwt:Issuers:0:PemKeyFile", jwtTokenIssuerFixture.PemFilepath},
            {"Jwt:Issuers:0:Type", jwtTokenIssuerFixture.KeyType},
        };

        using TestServerFixture<Startup> server = CreateServer(jwtConfiguration);

        // Act
        var result = await TestGrpc(jwtTokenIssuerFixture, server);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Jwt_MultipleIssuers_Valid()
    {
        // Arrange
        var jwtTokenIssuerFixture1 = new JwtTokenIssuerFixture() { Issuer = "Issuer1" };
        var jwtTokenIssuerFixture2 = new JwtTokenIssuerFixture() { Issuer = "Issuer2" };
        var invalidIssuer = new JwtTokenIssuerFixture() { Issuer = "Issuer3" };

        var jwtConfiguration = new Dictionary<string, string?>()
        {
            {"Jwt:Issuers:0:IssuerName", jwtTokenIssuerFixture1.Issuer},
            {"Jwt:Issuers:0:PemKeyFile", jwtTokenIssuerFixture1.PemFilepath},
            {"Jwt:Issuers:0:Type", jwtTokenIssuerFixture1.KeyType},
            {"Jwt:Issuers:1:IssuerName", jwtTokenIssuerFixture2.Issuer},
            {"Jwt:Issuers:1:PemKeyFile", jwtTokenIssuerFixture2.PemFilepath},
            {"Jwt:Issuers:1:Type", jwtTokenIssuerFixture2.KeyType},
        };

        using TestServerFixture<Startup> server = CreateServer(jwtConfiguration);

        // Test valid-1
        var result1 = await TestGrpc(jwtTokenIssuerFixture1, server);
        result1.Should().NotBeNull();

        // Test valid-2
        var result2 = await TestGrpc(jwtTokenIssuerFixture2, server);
        result2.Should().NotBeNull();

        // Test invalid
        var testMethod = () => TestGrpc(invalidIssuer, server);
        await testMethod.Should().ThrowAsync<RpcException>().WithMessage("Status(StatusCode=\"Unauthenticated\", Detail=\"Bad gRPC response. HTTP status code: 401\")");
    }

    [Fact]
    public async Task JwtVerification_NoAudienceCheck_Valid()
    {
        // Arrange
        var jwtTokenIssuerFixture = new JwtTokenIssuerFixture();

        var jwtConfiguration = new Dictionary<string, string?>()
        {
            {"Jwt:Issuers:0:IssuerName", jwtTokenIssuerFixture.Issuer},
            {"Jwt:Issuers:0:PemKeyFile", jwtTokenIssuerFixture.PemFilepath},
            {"Jwt:Issuers:0:Type", jwtTokenIssuerFixture.KeyType},
        };

        using TestServerFixture<Startup> server = CreateServer(jwtConfiguration);

        // Act
        var result = await TestGrpc(jwtTokenIssuerFixture, server);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task JwtVerification_InvalidAudience_401()
    {
        // Arrange
        var jwtTokenIssuerFixture = new JwtTokenIssuerFixture();

        var jwtConfiguration = new Dictionary<string, string?>()
        {
            {"Jwt:Audience", "InvalidAudience"},
            {"Jwt:Issuers:0:IssuerName", jwtTokenIssuerFixture.Issuer},
            {"Jwt:Issuers:0:PemKeyFile", jwtTokenIssuerFixture.PemFilepath},
            {"Jwt:Issuers:0:Type", jwtTokenIssuerFixture.KeyType},
        };

        using TestServerFixture<Startup> server = CreateServer(jwtConfiguration);

        // Act
        var testMethod = () => TestGrpc(jwtTokenIssuerFixture, server);

        // Assert
        await testMethod.Should().ThrowAsync<RpcException>().WithMessage("Status(StatusCode=\"Unauthenticated\", Detail=\"Bad gRPC response. HTTP status code: 401\")");
    }

    [Fact]
    public async Task JwtVerification_AllowAny()
    {
        // Arrange
        var jwtTokenIssuerFixture = new JwtTokenIssuerFixture();

        var jwtConfiguration = new Dictionary<string, string?>()
        {
            {"Jwt:Audience", "InvalidAudience"},
            {"Jwt:AllowAnyJwtToken", "true"},
        };

        using TestServerFixture<Startup> server = CreateServer(jwtConfiguration);
        server.GetTestLogger(_outputHelper);

        // Act
        var result = await TestGrpc(jwtTokenIssuerFixture, server);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task JwtVerification_InvalidConfig()
    {
        // Arrange
        var jwtTokenIssuerFixture = new JwtTokenIssuerFixture();

        var jwtConfiguration = new Dictionary<string, string?>()
        {
            {"Jwt:AllowAnyJwtToken", "false"},
        };

        using TestServerFixture<Startup> server = CreateServer(jwtConfiguration);
        server.GetTestLogger(_outputHelper);

        // Act
        var testMethod = () => TestGrpc(jwtTokenIssuerFixture, server);

        // Assert
        var rpcException = await testMethod.Should().ThrowAsync<RpcException>();
        rpcException.WithInnerException<NotSupportedException>().WithMessage("AllowAnyJwtToken is set to ”false” and no issuers are configured!");
    }

    [Fact]
    public async Task JwtVerification_InvalidType()
    {
        // Arrange
        var jwtTokenIssuerFixture = new JwtTokenIssuerFixture();

        var jwtConfiguration = new Dictionary<string, string?>()
        {
            {"Jwt:Issuers:0:IssuerName", jwtTokenIssuerFixture.Issuer},
            {"Jwt:Issuers:0:PemKeyFile", jwtTokenIssuerFixture.PemFilepath},
            {"Jwt:Issuers:0:Type", "invalidType"},
        };

        using TestServerFixture<Startup> server = CreateServer(jwtConfiguration);
        server.GetTestLogger(_outputHelper);

        // Act
        var testMethod = () => TestGrpc(jwtTokenIssuerFixture, server);

        // Assert
        await testMethod.Should().ThrowAsync<ValidationException>()
            .WithMessage("Issuer key could not be imported as type ”invalidType”, Issuer key type ”invalidType” not implemeted");
    }

    [Fact]
    public async Task JwtVerification_InvalidData()
    {
        // Arrange
        var jwtTokenIssuerFixture = new JwtTokenIssuerFixture();

        var fakeFilepath = Path.GetTempFileName();
        File.WriteAllText(fakeFilepath, "justSomeInvalidData");

        var jwtConfiguration = new Dictionary<string, string?>()
        {
            {"Jwt:Issuers:0:IssuerName", jwtTokenIssuerFixture.Issuer},
            {"Jwt:Issuers:0:PemKeyFile", fakeFilepath},
            {"Jwt:Issuers:0:Type", jwtTokenIssuerFixture.KeyType},
        };

        using TestServerFixture<Startup> server = CreateServer(jwtConfiguration);
        server.GetTestLogger(_outputHelper);

        // Act
        var testMethod = () => TestGrpc(jwtTokenIssuerFixture, server);

        // Assert
        await testMethod.Should().ThrowAsync<ValidationException>()
            .WithMessage("Issuer key could not be imported as type ”RSA”, No supported key formats were found. Check that the input represents the contents of a PEM-encoded key file, not the path to such a file. (Parameter 'input')");
    }

    [Fact]
    public async Task JwtVerification_Authority_Valid()
    {
        // Arrange
        var wireMockServer = WireMockServer.Start();
        var jwtTokenIssuerFixture = new JwtTokenIssuerFixture()
        {
            Issuer = wireMockServer.Urls[0]
        };

        wireMockServer.Given(Request.Create().WithPath("/.well-known/openid-configuration").UsingGet())
            .RespondWith(Response.Create().WithBody(jwtTokenIssuerFixture.GetJsonOpenIdConfiguration()));
        wireMockServer.Given(Request.Create().WithPath("/keys").UsingGet())
            .RespondWith(Response.Create().WithBody(jwtTokenIssuerFixture.GetJsonKeys()));

        var jwtConfiguration = new Dictionary<string, string?>()
        {
            {"Jwt:Authority", jwtTokenIssuerFixture.Issuer},
            {"Jwt:Audience", jwtTokenIssuerFixture.Audience},
            {"Jwt:RequireHttpsMetadata", "false"},
        };

        using TestServerFixture<Startup> server = CreateServer(jwtConfiguration);
        server.GetTestLogger(_outputHelper);

        // Act
        var result = await TestGrpc(jwtTokenIssuerFixture, server);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task JwtVerification_Authority_InvalidIssuer()
    {
        // Arrange
        var wireMockServer = WireMockServer.Start();
        var jwtTokenIssuerFixture = new JwtTokenIssuerFixture()
        {
            Issuer = wireMockServer.Urls[0]
        };

        wireMockServer.Given(Request.Create().WithPath("/.well-known/openid-configuration").UsingGet())
            .RespondWith(Response.Create().WithBody(jwtTokenIssuerFixture.GetJsonOpenIdConfiguration()));
        wireMockServer.Given(Request.Create().WithPath("/keys").UsingGet())
            .RespondWith(Response.Create().WithBody(jwtTokenIssuerFixture.GetJsonKeys()));

        var jwtConfiguration = new Dictionary<string, string?>()
        {
            {"Jwt:Authority", jwtTokenIssuerFixture.Issuer},
            {"Jwt:Audience", jwtTokenIssuerFixture.Audience},
            {"Jwt:RequireHttpsMetadata", "false"},
        };

        using TestServerFixture<Startup> server = CreateServer(jwtConfiguration);
        server.GetTestLogger(_outputHelper);

        var invalidIssuer = new JwtTokenIssuerFixture();

        // Act
        var testMethod = () => TestGrpc(invalidIssuer, server);

        // Assert
        await testMethod.Should().ThrowAsync<RpcException>().WithMessage("Status(StatusCode=\"Unauthenticated\", Detail=\"Bad gRPC response. HTTP status code: 401\")");
    }

    private static async Task<CreateWalletDepositEndpointResponse> TestGrpc(JwtTokenIssuerFixture jwtTokenIssuerFixture, TestServerFixture<Startup> server)
    {
        var (_, header) = jwtTokenIssuerFixture.GenerateUserHeader();
        var grpcClient = new WalletService.WalletServiceClient(server.Channel);
        var request = new CreateWalletDepositEndpointRequest();

        return await grpcClient.CreateWalletDepositEndpointAsync(request, header);
    }

    private TestServerFixture<Startup> CreateServer(Dictionary<string, string?> injectedConfig)
    {
        var server = new TestServerFixture<Startup>();

        var config = new Dictionary<string, string?>()
        {
            {"Otlp:Enabled", "false"},
            {"ConnectionStrings:Database", _dbFixture.ConnectionString},
            {"ServiceOptions:EndpointAddress", endpoint},
            {"VerifySlicesWorkerOptions:SleepTime", "00:00:02"},
        };

        config = config.Concat(injectedConfig).Concat(_messageBrokerFixture.Configuration).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        server.ConfigureHostConfiguration(config);
        return server;
    }
}
