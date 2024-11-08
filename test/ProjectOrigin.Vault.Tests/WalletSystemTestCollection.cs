using ProjectOrigin.Vault.Options;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProjectOrigin.Vault.Jobs;
using ProjectOrigin.Vault.Tests.Extensions;
using Xunit;
using System.Net.Http.Headers;
using System.Net.Http;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.Vault.Tests;

[CollectionDefinition(CollectionName)]
public class WalletSystemTestCollection : ICollectionFixture<WalletSystemTestFixture>
{
    public const string CollectionName = "WalletSystemTestCollection";
}

public class WalletSystemTestFixture : IAsyncLifetime
{
    private readonly string endpoint = "http://localhost/";
    private readonly TestServerFixture<Startup> _serverFixture;
    private readonly JwtTokenIssuerFixture _jwtTokenIssuerFixture;
    private readonly InMemoryFixture _inMemoryFixture;
    public PostgresDatabaseFixture DbFixture { get; }
    public StampAndRegistryFixture StampAndRegistryFixture { get; }

    public IHDAlgorithm Algorithm => _serverFixture.GetRequiredService<IHDAlgorithm>();

    public string StampUrl { get; set; } = "some-stamp-url";
    public string RegistryName { get; set; } = "some-registry-name";
    public string IssuerArea { get; set; } = "some-issuer-area";

    public int DaysBeforeCertificatesExpire { get; set; } = 60;
    public int ExpireCertificatesIntervalInSeconds { get; set; } = 5;

    public WalletSystemTestFixture()
    {
        _serverFixture = new TestServerFixture<Startup>();
        DbFixture = new PostgresDatabaseFixture();
        StampAndRegistryFixture = new StampAndRegistryFixture();
        _jwtTokenIssuerFixture = new JwtTokenIssuerFixture();
        _inMemoryFixture = new InMemoryFixture();
}

    public async Task InitializeAsync()
    {
        await DbFixture.InitializeAsync();
        await StampAndRegistryFixture.InitializeAsync();

        var networkOptions = new NetworkOptions
        {
            DaysBeforeCertificatesExpire = DaysBeforeCertificatesExpire
        };
        networkOptions.Registries.Add(StampAndRegistryFixture.RegistryName, new RegistryInfo
        {
            Url = StampAndRegistryFixture.RegistryUrl,
        });
        networkOptions.Areas.Add(StampAndRegistryFixture.IssuerArea, new AreaInfo
        {
            IssuerKeys = new List<KeyInfo>{
                new (){
                    PublicKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(StampAndRegistryFixture.IssuerKey.PublicKey.ExportPkixText()))
                }
            }
        });
        networkOptions.Issuers.Add(StampAndRegistryFixture.StampName, new IssuerInfo
        {
            StampUrl = StampAndRegistryFixture.StampUrl
        });
        StampUrl = StampAndRegistryFixture.StampUrl;
        RegistryName = StampAndRegistryFixture.RegistryName;
        IssuerArea = StampAndRegistryFixture.IssuerArea;

        var config = new Dictionary<string, string?>()
        {
            {"Otlp:Enabled", "false"},
            {"ConnectionStrings:Database", DbFixture.ConnectionString},
            {"ServiceOptions:EndpointAddress", endpoint},
            {"auth:type", "jwt"},
            {"auth:jwt:Audience", _jwtTokenIssuerFixture.Audience},
            {"auth:jwt:Issuers:0:IssuerName", _jwtTokenIssuerFixture.Issuer},
            {"auth:jwt:Issuers:0:Type", _jwtTokenIssuerFixture.KeyType},
            {"auth:jwt:Issuers:0:PemKeyFile", _jwtTokenIssuerFixture.PemFilepath},
            {"network:ConfigurationUri", networkOptions.ToTempYamlFileUri() },
            {"Retry:RegistryTransactionStillProcessingRetryCount", "5"},
            {"Retry:RegistryTransactionStillProcessingInitialIntervalSeconds", "1"},
            {"Retry:RegistryTransactionStillProcessingIntervalIncrementSeconds", "5"},
            {"Job:CheckForWithdrawnCertificatesIntervalInSeconds", "5"},
            {"Job:ExpireCertificatesIntervalInSeconds", ExpireCertificatesIntervalInSeconds.ToString()}
        };

        config = config.Concat(_inMemoryFixture.Configuration).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        _serverFixture.ConfigureHostConfiguration(config);
        _serverFixture.ConfigureTestServices += services => services.Remove(services.First(s => s.ImplementationType == typeof(PublishCheckForWithdrawnCertificatesCommandJob)));
    }

    public async Task DisposeAsync()
    {
        _serverFixture.Dispose();
        _jwtTokenIssuerFixture.Dispose();
        await DbFixture.DisposeAsync();
        await StampAndRegistryFixture.DisposeAsync();
    }

    public HttpClient CreateAuthenticatedHttpClient(string subject, string name, string[]? scopes = null)
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
