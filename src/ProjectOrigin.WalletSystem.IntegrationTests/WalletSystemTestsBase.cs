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

public abstract class WalletSystemTestsBase : IClassFixture<TestServerFixture<Startup>>, IClassFixture<PostgresDatabaseFixture>, IClassFixture<JwtTokenIssuerFixture>, IDisposable
{
    protected readonly string endpoint = "http://localhost/";
    protected readonly TestServerFixture<Startup> _serverFixture;
    protected readonly PostgresDatabaseFixture _dbFixture;
    protected readonly Fixture _fixture;

    private readonly JwtTokenIssuerFixture _jwtTokenIssuerFixture;
    private readonly IMessageBrokerFixture _messageBrokerFixture;
    private readonly IDisposable _logger;
    private bool _disposed = false;

    protected IHDAlgorithm Algorithm => _serverFixture.GetRequiredService<IHDAlgorithm>();

    public WalletSystemTestsBase(
        TestServerFixture<Startup> serverFixture,
        PostgresDatabaseFixture dbFixture,
        IMessageBrokerFixture messageBrokerFixture,
        JwtTokenIssuerFixture jwtTokenIssuerFixture,
        ITestOutputHelper outputHelper,
        RegistryFixture? registry)
    {
        _messageBrokerFixture = messageBrokerFixture;
        _jwtTokenIssuerFixture = jwtTokenIssuerFixture;
        _serverFixture = serverFixture;
        _dbFixture = dbFixture;
        _logger = serverFixture.GetTestLogger(outputHelper);

        _fixture = new Fixture();

        var config = new Dictionary<string, string?>()
        {
            {"Otlp:Enabled", "false"},
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

        serverFixture.ConfigureHostConfiguration(config);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _logger.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~WalletSystemTestsBase()
    {
        Dispose(false);
    }

    protected HttpClient CreateAuthenticatedHttpClient(string subject, string name)
    {
        var client = _serverFixture.CreateHttpClient();
        var token = _jwtTokenIssuerFixture.GenerateToken(subject, name);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected (string, Metadata) GenerateUserHeader()
    {
        return _jwtTokenIssuerFixture.GenerateUserHeader();
    }
}
