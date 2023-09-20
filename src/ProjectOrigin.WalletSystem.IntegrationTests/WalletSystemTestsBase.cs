using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
using System;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using System.Linq;
using MassTransit;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public abstract class WalletSystemTestsBase : IClassFixture<GrpcTestFixture<Startup>>, IClassFixture<PostgresDatabaseFixture>, IDisposable
{
    protected readonly string endpoint = "http://localhost/";
    private readonly IMessageBrokerFixture _messageBrokerFixture;
    protected readonly GrpcTestFixture<Startup> _grpcFixture;
    protected readonly PostgresDatabaseFixture _dbFixture;
    protected readonly JwtGenerator _tokenGenerator;
    private readonly IDisposable _logger;

    protected IHDAlgorithm Algorithm => _grpcFixture.GetRequiredService<IHDAlgorithm>();

    public WalletSystemTestsBase(
        GrpcTestFixture<Startup> grpcFixture,
        PostgresDatabaseFixture dbFixture,
        IMessageBrokerFixture messageBrokerFixture,
        ITestOutputHelper outputHelper,
        RegistryFixture? registry)
    {
        _messageBrokerFixture = messageBrokerFixture;
        _grpcFixture = grpcFixture;
        _dbFixture = dbFixture;
        _logger = grpcFixture.GetTestLogger(outputHelper);
        _tokenGenerator = new JwtGenerator();

        var config = new Dictionary<string, string?>()
        {
            {"ConnectionStrings:Database", dbFixture.ConnectionString},
            {"ServiceOptions:EndpointAddress", endpoint},
            {"VerifySlicesWorkerOptions:SleepTime", "00:00:02"},
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
}
