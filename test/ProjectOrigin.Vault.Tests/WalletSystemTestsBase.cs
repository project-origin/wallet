using AutoFixture;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.Vault.Options;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using ProjectOrigin.Vault.Jobs;
using ProjectOrigin.Vault.Tests.Extensions;
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

    public string StampUrl { get; set; } = "some-stamp-url";
    public string RegistryName { get; set; } = "some-registry-name";
    public string IssuerArea { get; set; } = "some-issuer-area";

    public WalletSystemTestsBase(
        TestServerFixture<Startup> serverFixture,
        PostgresDatabaseFixture dbFixture,
        IMessageBrokerFixture messageBrokerFixture,
        JwtTokenIssuerFixture jwtTokenIssuerFixture,
        ITestOutputHelper outputHelper,
        StampAndRegistryFixture? stampAndRegistryFixture)
    {
        _messageBrokerFixture = messageBrokerFixture;
        _jwtTokenIssuerFixture = jwtTokenIssuerFixture;
        _serverFixture = serverFixture;
        _dbFixture = dbFixture;
        _logger = serverFixture.GetTestLogger(outputHelper);

        _fixture = new Fixture();

        var networkOptions = new NetworkOptions
        {
            DaysBeforeCertificatesExpire = 60
        };
        if (stampAndRegistryFixture is not null)
        {
            networkOptions.Registries.Add(stampAndRegistryFixture.RegistryName, new RegistryInfo
            {
                Url = stampAndRegistryFixture.RegistryUrl,
            });
            networkOptions.Areas.Add(stampAndRegistryFixture.IssuerArea, new AreaInfo
            {
                IssuerKeys = new List<KeyInfo>{
                        new (){
                            PublicKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(stampAndRegistryFixture.IssuerKey.PublicKey.ExportPkixText()))
                        }
                    }
            });
            networkOptions.Issuers.Add(stampAndRegistryFixture.StampName, new IssuerInfo
            {
                StampUrl = stampAndRegistryFixture.StampUrl
            });
            StampUrl = stampAndRegistryFixture.StampUrl;
            RegistryName = stampAndRegistryFixture.RegistryName;
            IssuerArea = stampAndRegistryFixture.IssuerArea;
        }

        var config = new Dictionary<string, string?>()
        {
            {"Otlp:Enabled", "false"},
            {"ConnectionStrings:Database", dbFixture.ConnectionString},
            {"ServiceOptions:EndpointAddress", endpoint},
            {"auth:type", "jwt"},
            {"auth:jwt:Audience", jwtTokenIssuerFixture.Audience},
            {"auth:jwt:Issuers:0:IssuerName", jwtTokenIssuerFixture.Issuer},
            {"auth:jwt:Issuers:0:Type", jwtTokenIssuerFixture.KeyType},
            {"auth:jwt:Issuers:0:PemKeyFile", jwtTokenIssuerFixture.PemFilepath},
            {"network:ConfigurationUri", networkOptions.ToTempYamlFileUri() },
            {"Retry:RegistryTransactionStillProcessingRetryCount", "5"},
            {"Retry:RegistryTransactionStillProcessingInitialIntervalSeconds", "1"},
            {"Retry:RegistryTransactionStillProcessingIntervalIncrementSeconds", "5"},
            {"Job:CheckForWithdrawnCertificatesIntervalInSeconds", "5"},
            {"Job:ExpireCertificatesIntervalInSeconds", "5"}
        };

        config = config.Concat(_messageBrokerFixture.Configuration).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        serverFixture.ConfigureHostConfiguration(config);
        serverFixture.ConfigureTestServices += services => services.Remove(services.First(s => s.ImplementationType == typeof(PublishCheckForWithdrawnCertificatesCommandJob)));

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

    public HttpClient CreateStampClient()
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri(StampUrl);
        return client;
    }
}
