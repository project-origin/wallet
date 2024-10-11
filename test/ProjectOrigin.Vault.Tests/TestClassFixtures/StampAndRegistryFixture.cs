using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;

namespace ProjectOrigin.Vault.Tests.TestClassFixtures;

public class StampAndRegistryFixture : RegistryFixture
{
    private readonly Lazy<IContainer> _stampContainer;
    private readonly PostgreSqlContainer _stampPostgresContainer;

    private const int StampHttpPort = 5000;
    private const string StampPathBase = "/stamp-api";

    public string StampName => "narnia-stamp";

    public StampAndRegistryFixture()
    {
        _stampPostgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15.2")
            .WithNetwork(Network)
            .WithDatabase("postgres")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithPortBinding(5432, true)
            .Build();

        _stampContainer = new Lazy<IContainer>(() =>
        {
            var connectionString = $"Host={_stampPostgresContainer.IpAddress};Port=5432;Database=postgres;Username=postgres;Password=postgres";

            // Get an available port from system and use that as the host port
            var udp = new UdpClient(0, AddressFamily.InterNetwork);
            var hostPort = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;

            return new ContainerBuilder()
                .WithImage("ghcr.io/project-origin/stamp:4.0.0")
                .WithNetwork(Network)
                .WithPortBinding(hostPort, StampHttpPort)
                .WithCommand("--serve", "--migrate")
                .WithEnvironment("RestApiOptions__PathBase", StampPathBase)
                .WithEnvironment("Otlp__Enabled", "false")
                .WithEnvironment("Retry__DefaultFirstLevelRetryCount", "5")
                .WithEnvironment("Retry__RegistryTransactionStillProcessingRetryCount", "10")
                .WithEnvironment("Retry__RegistryTransactionStillProcessingInitialIntervalSeconds", "1")
                .WithEnvironment("Retry__RegistryTransactionStillProcessingIntervalIncrementSeconds", "5")
                .WithEnvironment($"Registries__0__name", RegistryName)
                .WithEnvironment($"Registries__0__address", RegistryUrlWithinNetwork)
                .WithEnvironment($"IssuerPrivateKeyPems__{IssuerArea}", Convert.ToBase64String(Encoding.UTF8.GetBytes(IssuerKey.ExportPkixText())))
                .WithEnvironment("ConnectionStrings__Database", connectionString)
                .WithEnvironment("MessageBroker__Type", "InMemory")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(StampHttpPort))
                //.WithEnvironment("Logging__LogLevel__Default", "Trace")
                .Build();
        });
    }

    public string StampUrl =>
        new UriBuilder("http", _stampContainer.Value.Hostname, _stampContainer.Value.GetMappedPublicPort(StampHttpPort), StampPathBase).Uri.ToString();

    public override async Task InitializeAsync()
    {
        await Task.WhenAll(base.InitializeAsync(), _stampPostgresContainer.StartAsync());
        await Task.WhenAll(_stampContainer.Value.StartAsync());
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();

        await Task.WhenAll(
            _stampPostgresContainer.DisposeAsync().AsTask(),
            _stampContainer.Value.DisposeAsync().AsTask());
    }
}
