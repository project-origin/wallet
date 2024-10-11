using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Npgsql;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using Xunit;
using Xunit.Abstractions;
using ProjectOrigin.Vault.Options;
using System.Net.Http;
using Testcontainers.PostgreSql;
using ProjectOrigin.Vault.Tests.Extensions;

namespace ProjectOrigin.Vault.Tests;

public class WithdrawCertificatesTests2 : IAsyncLifetime,
    IClassFixture<ContainerImageFixture>,
    IClassFixture<StampAndRegistryFixture>
{
    private readonly Lazy<IContainer> _walletContainer;
    private readonly PostgreSqlContainer _postgresFixture;
    private readonly ITestOutputHelper _outputHelper;
    private readonly StampAndRegistryFixture _stampAndRegistryFixture;

    private const int WalletHttpPort = 5000;
    private const string WalletAlias = "wallet-container";
    private const string PathBase = "/wallet-api";
    private const string WalletPostgresAlias = "wallet-postgres";

    public WithdrawCertificatesTests2(
        ContainerImageFixture imageFixture,
        ITestOutputHelper outputHelper,
        StampAndRegistryFixture stampAndRegistryFixture)
    {
        _outputHelper = outputHelper;
        _stampAndRegistryFixture = stampAndRegistryFixture;

        _postgresFixture = new PostgreSqlBuilder()
            .WithImage("postgres:15")
            .WithNetwork(_stampAndRegistryFixture.Network)
            .WithNetworkAliases(WalletPostgresAlias)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        var networkOptions = new NetworkOptions();
        networkOptions.Registries.Add(_stampAndRegistryFixture.RegistryName, new RegistryInfo
        {
            Url = _stampAndRegistryFixture.RegistryUrlWithinNetwork,
        });
        networkOptions.Areas.Add(_stampAndRegistryFixture.IssuerArea, new AreaInfo
        {
            IssuerKeys = new List<KeyInfo>{
                new (){
                    PublicKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(_stampAndRegistryFixture.IssuerKey.PublicKey.ExportPkixText()))
                }
            }
        });
        networkOptions.Stamps.Add(_stampAndRegistryFixture.StampName, new StampInfo
        {
            Url = _stampAndRegistryFixture.StampUrl
        });

        var configFile = networkOptions.ToTempYamlFile();

        _walletContainer = new Lazy<IContainer>(() => new ContainerBuilder()
            .WithImage(imageFixture.Image)
            .WithName(WalletAlias + Guid.NewGuid())
            .WithNetwork(_stampAndRegistryFixture.Network)
            .WithNetworkAliases(WalletAlias)
            .WithResourceMapping(configFile, "/app/tmp/")
            .WithPortBinding(WalletHttpPort, true)
            .WithCommand("--serve", "--migrate")
            .WithEnvironment("Otlp__Enabled", "false")
            .WithEnvironment("ConnectionStrings__Database", _postgresFixture.GetLocalConnectionString(WalletPostgresAlias))
            .WithEnvironment("ServiceOptions__EndpointAddress", $"http://{WalletAlias}:{WalletHttpPort}/")
            .WithEnvironment("ServiceOptions__PathBase", PathBase)
            .WithEnvironment("auth__type", "jwt")
            .WithEnvironment("auth__jwt__AllowAnyJwtToken", "true")
            .WithEnvironment("network__ConfigurationUri", "file:///app/tmp/" + Path.GetFileName(configFile))
            .WithEnvironment("Retry__RegistryTransactionStillProcessingRetryCount", "5")
            .WithEnvironment("Retry__RegistryTransactionStillProcessingInitialIntervalSeconds", "1")
            .WithEnvironment("Retry__RegistryTransactionStillProcessingIntervalIncrementSeconds", "5")
            .WithEnvironment("Job__CheckForWithdrawnCertificatesIntervalInSeconds", "5")
            .WithEnvironment("MessageBroker__Type", "InMemory")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(WalletHttpPort))
            //.WithEnvironment("Logging__LogLevel__Default", "Trace")
            .Build());
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _stampAndRegistryFixture.InitializeAsync();
            await _postgresFixture.StartAsync();
            await _walletContainer.Value.StartAsync();
        }
        catch (Exception)
        {
            await WriteRegistryContainerLog();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (_walletContainer.IsValueCreated)
        {
            await WriteRegistryContainerLog();
            await _walletContainer.Value.StopAsync();
            await _postgresFixture.StopAsync();
        }
        await _stampAndRegistryFixture.DisposeAsync();
    }

    private async Task WriteRegistryContainerLog()
    {
        var log = await _walletContainer.Value.GetLogsAsync();
        _outputHelper.WriteLine($"-------Container stdout------\n{log.Stdout}\n-------Container stderr------\n{log.Stderr}\n\n----------");
    }

    protected HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri($"http://{_walletContainer.Value.IpAddress}:{_walletContainer.Value.GetMappedPublicPort(WalletHttpPort)}");
        return client;
    }

    public HttpClient CreateStampClient()
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri(_stampAndRegistryFixture.StampUrl);
        return client;
    }

    [Fact]
    public async Task WithdrawCertificate()
    {
        var registryName = _stampAndRegistryFixture.RegistryName;
        var issuerArea = _stampAndRegistryFixture.IssuerArea;

        var walletClient = CreateHttpClient();
        var wResponse = await walletClient.CreateWallet();
        var weResponse = await walletClient.CreateWalletEndpoint(wResponse.WalletId);

        var stampClient = CreateStampClient();
        var rResponse = await stampClient.StampCreateRecipient(new CreateRecipientRequest
        {
            WalletEndpointReference = new StampWalletEndpointReferenceDto
            {
                Version = weResponse.WalletReference.Version,
                Endpoint = weResponse.WalletReference.Endpoint,
                PublicKey = weResponse.WalletReference.PublicKey.Export().ToArray()
            }
        });

        var gsrn = Some.Gsrn();
        var certificateId = Guid.NewGuid();
        var icResponse = await stampClient.StampIssueCertificate(new CreateCertificateRequest
        {
            RecipientId = rResponse.Id,
            RegistryName = registryName,
            MeteringPointId = gsrn,
            Certificate = new StampCertificateDto
            {
                Id = certificateId,
                Start = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                End = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
                GridArea = issuerArea,
                Quantity = 123,
                Type = StampCertificateType.Production,
                ClearTextAttributes = new Dictionary<string, string>
                {
                    { "fuelCode", Some.FuelCode },
                    { "techCode", Some.TechCode }
                },
                HashedAttributes = new List<StampHashedAttribute>
                {
                    new () { Key = "assetId", Value = gsrn },
                    new () { Key = "address", Value = "Some road 1234" }
                }
            }
        });

        await Task.Delay(TimeSpan.FromSeconds(30)); //wait for cert to be on registry and sent back to the wallet

        var withdrawResponse = await stampClient.StampWithdrawCertificate(registryName, certificateId);

        using (var connection = new NpgsqlConnection(_postgresFixture.GetConnectionString()))
        {
            //TODO the query below is wrong
            var withdrawnSlice = await connection.RepeatedlyQueryFirstOrDefaultUntil<WalletSlice>(@"SELECT *
                  FROM certificates
                  WHERE registry_name = @registry
                  AND id = @certificateId
                  AND withdrawn = true",
                new
                {
                    registry = registryName,
                    certificateId
                }, timeLimit: TimeSpan.FromSeconds(45));

            withdrawnSlice.Should().NotBeNull();
            withdrawnSlice.RegistryName.Should().Be(registryName);
            withdrawnSlice.CertificateId.Should().Be(certificateId);
        }
    }
}
