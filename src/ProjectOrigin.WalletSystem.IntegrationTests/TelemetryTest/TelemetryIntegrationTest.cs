using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using MassTransit.Logging;
using MassTransit.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using WireMock.RequestBuilders;
using WireMock.Server;
using Xunit;
using Response = WireMock.ResponseBuilders.Response;

namespace ProjectOrigin.WalletSystem.IntegrationTests.TelemetryTest;

public class TelemetryIntegrationTest : IClassFixture<TestServerFixture<Startup>>,
    IClassFixture<InMemoryFixture>,
    IClassFixture<PostgresDatabaseFixture>,
    IClassFixture<JwtTokenIssuerFixture>, IDisposable
{
    private readonly TestServerFixture<Startup> _serverFixture;
    private readonly WireMockServer _wireMockServer;

    public TelemetryIntegrationTest(
        TestServerFixture<Startup> serverFixture,
        InMemoryFixture inMemoryFixture,
        PostgresDatabaseFixture dbFixture,
        JwtTokenIssuerFixture jwtTokenIssuerFixture)
    {
        _serverFixture = serverFixture;
        var jwtTokenIssuerFixture1 = jwtTokenIssuerFixture;

        _wireMockServer = WireMockServer.Start();
        _wireMockServer.Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200));
        var combinedConfiguration = new Dictionary<string, string?>(inMemoryFixture.Configuration)
        {
            ["Otlp:Enabled"] = "true",
            ["Otlp:Endpoint"] = _wireMockServer.Urls[0],
            ["ConnectionStrings:Database"] = dbFixture.ConnectionString,
            ["ServiceOptions:EndpointAddress"] = "http://localhost/",
            ["Jwt:Audience"] = jwtTokenIssuerFixture1.Audience,
            ["Jwt:Issuer:0:IssuerName"] = jwtTokenIssuerFixture1.Issuer,
            ["Jwt:Issuers:0:PemKeyFile"] = jwtTokenIssuerFixture1.PemFilepath,
            ["Jwt:Issuers:0:Type"] = "ecdsa"
        };

        serverFixture.ConfigureHostConfiguration(combinedConfiguration);

        serverFixture.ConfigureTestServices += services =>
        {
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(serviceName: "Wallet.Test"))
                .WithMetrics(metrics => metrics
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = new Uri(_wireMockServer.Urls[0])))
                .WithTracing(provider =>
                    provider
                        .AddHttpClientInstrumentation()
                        .AddOtlpExporter(o =>
                        {
                            o.Endpoint = new Uri(_wireMockServer.Urls[0]);
                            o.Protocol = OtlpExportProtocol.HttpProtobuf;
                            o.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>()
                            {
                                MaxQueueSize = 2,
                                ScheduledDelayMilliseconds = 1000,
                                MaxExportBatchSize = 1
                            };
                            o.HttpClientFactory = () =>
                            {
                                HttpClient client = new HttpClient();
                                client.DefaultRequestHeaders.Add("X-TestHeader", "value");
                                return client;
                            };
                        }));

        };
    }

    [Fact]
    public async Task TelemetryData_ShouldBeSentToMockCollector()
    {
        var client = _serverFixture.CreateHttpClient();

        await client.GetAsync("/health");

        await Task.Delay(1100);

        var incomingRequests = _wireMockServer.LogEntries
            .ToList();

        incomingRequests.Should().NotBeEmpty();
    }

    public void Dispose()
    {
        _wireMockServer.Stop();
        _wireMockServer.Dispose();
    }
}
