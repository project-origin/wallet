using DotNet.Testcontainers.Builders;
using ProjectOrigin.Vault.Options;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using ProjectOrigin.Vault.Tests.Extensions;
using Testcontainers.PostgreSql;
using Xunit;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using System.Net.Http.Headers;
using System.Net.Http;

namespace ProjectOrigin.Vault.Tests;

[CollectionDefinition(CollectionName)]
public class DockerTestCollection : ICollectionFixture<DockerTestFixture>
{
    public const string CollectionName = "DockerTestCollection";
}

public class DockerTestFixture : IAsyncLifetime
{
    public ContainerImageFixture ImageFixture { get; private set; }
    public StampAndRegistryFixture StampAndRegistryFixture { get; private set; }
    public PostgreSqlContainer PostgresFixture { get; private set; }
    public JwtTokenIssuerFixture JwtTokenIssuerFixture { get; private set; }
    public Lazy<IContainer> WalletContainer;


    public int WalletHttpPort = 5000;
    public string WalletAlias = "wallet-container";
    public string PathBase = "/wallet-api";
    public string WalletPostgresAlias = "wallet-postgres";

    public int? DaysBeforeCertificatesExpire { get; } = 60;
    public int ExpireCertificatesIntervalInSeconds { get; } = 5;
    public IHDAlgorithm Algorithm => new Secp256k1Algorithm();
    public DockerTestFixture()
    {
        ImageFixture = new ContainerImageFixture();
        StampAndRegistryFixture = new StampAndRegistryFixture();
        JwtTokenIssuerFixture = new JwtTokenIssuerFixture();

        PostgresFixture = new PostgreSqlBuilder()
            .WithImage("postgres:15")
            .WithNetwork(StampAndRegistryFixture.Network)
            .WithNetworkAliases(WalletPostgresAlias)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        var networkOptions = new NetworkOptions
        {
            DaysBeforeCertificatesExpire = DaysBeforeCertificatesExpire
        };
        networkOptions.Registries.Add(StampAndRegistryFixture.RegistryName, new RegistryInfo
        {
            Url = StampAndRegistryFixture.RegistryUrlWithinNetwork,
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
            StampUrl = StampAndRegistryFixture.StampUrlInNetwork
        });

        var configFile = networkOptions.ToTempYamlFile();

        WalletContainer = new Lazy<IContainer>(() => new ContainerBuilder()
            .WithImage(ImageFixture.Image)
            .WithName(WalletAlias + Guid.NewGuid())
            .WithNetwork(StampAndRegistryFixture.Network)
            .WithNetworkAliases(WalletAlias)
            .WithResourceMapping(configFile, "/app/tmp/")
            .WithPortBinding(WalletHttpPort, true)
            .WithCommand("--serve", "--migrate")
            .WithEnvironment("Otlp__Enabled", "false")
            .WithEnvironment("ConnectionStrings__Database", PostgresFixture.GetLocalConnectionString(WalletPostgresAlias))
            .WithEnvironment("ServiceOptions__EndpointAddress", $"http://{WalletAlias}:{WalletHttpPort}/")
            .WithEnvironment("ServiceOptions__PathBase", PathBase)
            .WithEnvironment("auth__type", "jwt")
            .WithEnvironment("auth__jwt__AllowAnyJwtToken", "true")
            .WithEnvironment("network__ConfigurationUri", "file:///app/tmp/" + Path.GetFileName(configFile))
            .WithEnvironment("Retry__RegistryTransactionStillProcessingRetryCount", "5")
            .WithEnvironment("Retry__RegistryTransactionStillProcessingInitialIntervalSeconds", "1")
            .WithEnvironment("Retry__RegistryTransactionStillProcessingIntervalIncrementSeconds", "5")
            .WithEnvironment("Job__CheckForWithdrawnCertificatesIntervalInSeconds", "5")
            .WithEnvironment("Job__ExpireCertificatesIntervalInSeconds", ExpireCertificatesIntervalInSeconds.ToString())
            .WithEnvironment("MessageBroker__Type", "InMemory")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(WalletHttpPort))
            //.WithEnvironment("Logging__LogLevel__Default", "Trace")
            .Build());
    }


    public async Task InitializeAsync()
    {
        await ImageFixture.InitializeAsync();
        await StampAndRegistryFixture.InitializeAsync();
        await PostgresFixture.StartAsync();
        await WalletContainer.Value.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (WalletContainer.IsValueCreated)
        {
            await WalletContainer.Value.StopAsync();
            await PostgresFixture.StopAsync();
            await ImageFixture.DisposeAsync();
            JwtTokenIssuerFixture.Dispose();
        }
    }

    public HttpClient CreateAuthenticatedHttpClient(string subject, string name, string[]? scopes = null)
    {
        var client = new HttpClient();
        client.BaseAddress = new UriBuilder("http",
            WalletContainer.Value.Hostname,
            WalletContainer.Value.GetMappedPublicPort(WalletHttpPort),
            PathBase).Uri;
        var token = JwtTokenIssuerFixture.GenerateToken(subject, name, scopes);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateStampClient()
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri(StampAndRegistryFixture.StampUrl);
        return client;
    }
}
