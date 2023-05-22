using ProjectOrigin.Wallet.IntegrationTests.TestClassFixtures;
using ProjectOrigin.Wallet.Server.Database;
using ProjectOrigin.Wallet.Server;
using System.Collections.Generic;
using ProjectOrigin.Wallet.Server.HDWallet;
using Xunit;
using AutoFixture;

namespace ProjectOrigin.Wallet.IntegrationTests;

public abstract class GrpcTestsBase : IClassFixture<GrpcTestFixture<Startup>>, IClassFixture<PostgresDatabaseFixture>
{
    private readonly string endpoint = "http://my-endpoint:80/";

    protected readonly GrpcTestFixture<Startup> _grpcFixture;
    protected readonly PostgresDatabaseFixture _dbFixture;
    protected JwtGenerator _tokenGenerator;
    protected Fixture _fixture;

    protected const string RegistryName = "RegistryA";
    protected IHDAlgorithm _algorithm;

    public GrpcTestsBase(GrpcTestFixture<Startup> grpcFixture, PostgresDatabaseFixture dbFixture)
    {
        _grpcFixture = grpcFixture;
        _dbFixture = dbFixture;
        this._tokenGenerator = new JwtGenerator();
        this._fixture = new Fixture();

        DatabaseUpgrader.Upgrade(dbFixture.ConnectionString);
        grpcFixture.ConfigureHostConfiguration(new Dictionary<string, string?>()
        {
            {"ConnectionStrings:Database", dbFixture.ConnectionString},
            {"ServiceOptions:EndpointAddress", endpoint}
        });

        _algorithm = new Secp256k1Algorithm();
    }
}

