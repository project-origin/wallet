using AutoFixture;
using Grpc.Core;
using MassTransit;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public abstract class WalletSystemTestsBase : IClassFixture<GrpcTestFixture<Startup>>, IClassFixture<PostgresDatabaseFixture>, IClassFixture<JwtTokenIssuerFixture>, IDisposable
{
    protected readonly string endpoint = "http://localhost/";
    protected readonly GrpcTestFixture<Startup> _grpcFixture;
    protected readonly PostgresDatabaseFixture _dbFixture;
    protected readonly Fixture _fixture;

    private readonly JwtTokenIssuerFixture _jwtTokenIssuerFixture;
    private readonly IMessageBrokerFixture _messageBrokerFixture;
    private readonly IDisposable _logger;

    protected IHDAlgorithm Algorithm => _grpcFixture.GetRequiredService<IHDAlgorithm>();

    public WalletSystemTestsBase(
        GrpcTestFixture<Startup> grpcFixture,
        PostgresDatabaseFixture dbFixture,
        IMessageBrokerFixture messageBrokerFixture,
        JwtTokenIssuerFixture jwtTokenIssuerFixture,
        ITestOutputHelper outputHelper,
        RegistryFixture? registry)
    {
        _messageBrokerFixture = messageBrokerFixture;
        _jwtTokenIssuerFixture = jwtTokenIssuerFixture;
        _grpcFixture = grpcFixture;
        _dbFixture = dbFixture;
        _logger = grpcFixture.GetTestLogger(outputHelper);

        _fixture = new Fixture();

        var config = new Dictionary<string, string?>()
        {
            {"ConnectionStrings:Database", dbFixture.ConnectionString},
            {"ServiceOptions:EndpointAddress", endpoint},
            {"VerifySlicesWorkerOptions:SleepTime", "00:00:02"},
            {"Jwt:Audience", jwtTokenIssuerFixture.Audience},
            {"Jwt:Issuers:0:IssuerName", jwtTokenIssuerFixture.Issuer},
            {"Jwt:Issuers:0:Type", "ecdsa"},
            {"Jwt:Issuers:0:PemKeyFile", jwtTokenIssuerFixture.PemFilepath}
        };

        config = config.Concat(_messageBrokerFixture.Configuration).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (registry is not null)
            config.Add($"RegistryUrls:{registry.Name}", registry.RegistryUrl);

        grpcFixture.ConfigureHostConfiguration(config);
    }

    public void Dispose()
    {
        _logger.Dispose();
    }

    protected HttpClient CreateAuthenticatedHttpClient(string subject, string name)
    {
        var client = _grpcFixture.CreateHttpClient();
        var token = _jwtTokenIssuerFixture.GenerateToken(subject, name);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected (string, Metadata) GenerateUserHeader()
    {
        var subject = _fixture.Create<string>();
        var name = _fixture.Create<string>();

        var token = _jwtTokenIssuerFixture.GenerateToken(subject, name);

        var headers = new Metadata
        {
            { "Authorization", $"Bearer {token}" }
        };

        return (subject, headers);
    }
}
