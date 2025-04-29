using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FluentAssertions;
using ProjectOrigin.Vault.Options;
using ProjectOrigin.Vault.Tests.Extensions;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using ProjectOrigin.Vault.Tests.TestExtensions;
using Xunit;

namespace ProjectOrigin.Vault.Tests.TelemetryTest;

public class TelemetryIntegrationTest :
    IClassFixture<TestServerFixture<Startup>>,
    IClassFixture<InMemoryFixture>,
    IClassFixture<PostgresDatabaseFixture>,
    IClassFixture<OpenTelemetryFixture>,
    IClassFixture<JwtTokenIssuerFixture>
{
    private readonly TestServerFixture<Startup> _serverFixture;
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly JwtTokenIssuerFixture _jwtTokenIssuerFixture;
    private OpenTelemetryFixture _openTelemetryFixture;

    public TelemetryIntegrationTest(
        TestServerFixture<Startup> serverFixture,
        InMemoryFixture inMemoryFixture,
        PostgresDatabaseFixture dbFixture,
        OpenTelemetryFixture openTelemetryFixture,
        JwtTokenIssuerFixture jwtTokenIssuerFixture)
    {
        _serverFixture = serverFixture;
        _dbFixture = dbFixture;
        _jwtTokenIssuerFixture = jwtTokenIssuerFixture;
        _openTelemetryFixture = openTelemetryFixture;

        var combinedConfiguration = new Dictionary<string, string?>(inMemoryFixture.Configuration)
        {
            {"network:ConfigurationUri", new NetworkOptions { DaysBeforeCertificatesExpire = 60 }.ToTempYamlFileUri() },
            {"Otlp:Enabled", "true"},
            {"Otlp:Endpoint", openTelemetryFixture.OtelUrl},
            {"ConnectionStrings:Database", dbFixture.ConnectionString},
            {"ServiceOptions:EndpointAddress", "http://localhost/"},
            {"auth:type", "jwt"},
            {"auth:jwt:Audience", jwtTokenIssuerFixture.Audience},
            {"auth:jwt:Issuers:0:IssuerName", jwtTokenIssuerFixture.Issuer},
            {"auth:jwt:Issuers:0:PemKeyFile", jwtTokenIssuerFixture.PemFilepath},
            {"auth:jwt:Issuers:0:Type", jwtTokenIssuerFixture.KeyType},
            {"Job:CheckForWithdrawnCertificatesIntervalInSeconds", "5"},
            {"Job:ExpireCertificatesIntervalInSeconds", "5"}
        };

        serverFixture.ConfigureHostConfiguration(combinedConfiguration);
    }

    [Fact]
    public async Task TelemetryData_ShouldBeSentToMockCollector()
    {
        var subject = Guid.NewGuid().ToString();
        var name = "John Doe'";

        var client = _serverFixture.CreateHttpClient();
        var token = _jwtTokenIssuerFixture.GenerateToken(subject, name);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await _dbFixture.CreateWallet(subject);

        var getCertResult = await client.GetAsync("v1/certificates");
        getCertResult.EnsureSuccessStatusCode();

        await Task.Delay(10000);
        var telemetryData = await _openTelemetryFixture.GetContainerLog();

        telemetryData.Should().NotBeNullOrWhiteSpace();
        telemetryData.Should().Contain("v1/certificates");
    }
}
