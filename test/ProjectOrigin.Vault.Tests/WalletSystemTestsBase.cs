using AutoFixture;
using MassTransit;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.Vault.Options;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.Vault.Tests;

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

        var networkOptions = new NetworkOptions();
        if (registry is not null)
        {
            networkOptions.Registries.Add(registry.Name, new RegistryInfo
            {
                Url = registry.RegistryUrl,
            });
            networkOptions.Areas.Add(registry.IssuerArea, new AreaInfo
            {
                IssuerKeys = new List<KeyInfo>{
                        new (){
                            PublicKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(registry.IssuerKey.PublicKey.ExportPkixText()))
                        }
                    }
            });
        }

        var config = new Dictionary<string, string?>()
        {
            {"Otlp:Enabled", "false"},
            {"ConnectionStrings:Database", dbFixture.ConnectionString},
            {"ServiceOptions:EndpointAddress", endpoint},
            {"VerifySlicesWorkerOptions:SleepTime", "00:00:02"},
            {"auth:type", "jwt"},
            {"auth:jwt:Audience", jwtTokenIssuerFixture.Audience},
            {"auth:jwt:Issuers:0:IssuerName", jwtTokenIssuerFixture.Issuer},
            {"auth:jwt:Issuers:0:Type", jwtTokenIssuerFixture.KeyType},
            {"auth:jwt:Issuers:0:PemKeyFile", jwtTokenIssuerFixture.PemFilepath},
            {"network:ConfigurationUri", networkOptions.ToTempFileUri() }
        };

        config = config.Concat(_messageBrokerFixture.Configuration).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
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

    protected HttpClient CreateAuthenticatedHttpClient(string subject, string name, string[]? scopes = null)
    {
        var client = _serverFixture.CreateHttpClient();
        var token = _jwtTokenIssuerFixture.GenerateToken(subject, name, scopes);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
