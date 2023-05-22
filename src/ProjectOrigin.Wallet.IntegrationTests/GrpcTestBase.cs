using ProjectOrigin.Wallet.IntegrationTests.TestClassFixtures;
using ProjectOrigin.Wallet.Server;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
using System;

namespace ProjectOrigin.Wallet.IntegrationTests;

public abstract class GrpcTestsBase : IClassFixture<GrpcTestFixture<Startup>>, IClassFixture<PostgresDatabaseFixture>, IDisposable
{
    protected readonly GrpcTestFixture<Startup> _grpcFixture;
    protected readonly PostgresDatabaseFixture _dbFixture;
    private readonly IDisposable _logger;

    public GrpcTestsBase(GrpcTestFixture<Startup> grpcFixture, PostgresDatabaseFixture dbFixture, ITestOutputHelper outputHelper)
    {
        _grpcFixture = grpcFixture;
        _dbFixture = dbFixture;
        _logger = grpcFixture.GetTestLogger(outputHelper);

        grpcFixture.ConfigureHostConfiguration(new Dictionary<string, string?>()
         {
             {"ConnectionStrings:Database", dbFixture.ConnectionString}
         });
    }

    public void Dispose()
    {
        _logger.Dispose();
    }
}
