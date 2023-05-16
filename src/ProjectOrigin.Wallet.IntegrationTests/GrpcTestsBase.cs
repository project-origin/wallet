using ProjectOrigin.Wallet.IntegrationTests.TestClassFixtures;
using ProjectOrigin.Wallet.Server.Database;
using ProjectOrigin.Wallet.Server;
using System.Collections.Generic;
using Xunit;

namespace ProjectOrigin.Wallet.IntegrationTests;

public abstract class GrpcTestsBase : IClassFixture<GrpcTestFixture<Startup>>, IClassFixture<PostgresDatabaseFixture>
{
    private readonly string endpoint = "http://my-endpoint:80/";

    protected readonly GrpcTestFixture<Startup> _grpcFixture;
    protected readonly PostgresDatabaseFixture _dbFixture;
    protected JwtGenerator _tokenGenerator;

    public GrpcTestsBase(GrpcTestFixture<Startup> grpcFixture, PostgresDatabaseFixture dbFixture)
    {
        _grpcFixture = grpcFixture;
        _dbFixture = dbFixture;
        this._tokenGenerator = new JwtGenerator();

        DatabaseUpgrader.Upgrade(dbFixture.ConnectionString);
        grpcFixture.ConfigureHostConfiguration(new Dictionary<string, string?>()
        {
            {"ConnectionStrings:Database", dbFixture.ConnectionString},
            {"ServiceOptions:EndpointAddress", endpoint}
        });
    }
}

