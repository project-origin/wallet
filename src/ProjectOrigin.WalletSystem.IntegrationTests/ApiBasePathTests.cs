using System;
using System.Net;
using System.Threading.Tasks;
using AutoFixture;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using ProjectOrigin.WalletSystem.Server.Database.Mapping;
using ProjectOrigin.WalletSystem.Server.Options;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class ApiBasePathTests : WalletSystemTestsBase, IClassFixture<InMemoryFixture>, IDisposable
{
    private readonly PathString _basePath = "/foo-bar-baz";

    public ApiBasePathTests(
        GrpcTestFixture<Startup> grpcFixture,
        PostgresDatabaseFixture dbFixture,
        InMemoryFixture inMemoryFixture,
        ITestOutputHelper outputHelper)
        : base(
            grpcFixture,
            dbFixture,
            inMemoryFixture,
            outputHelper,
            null)
    {
        grpcFixture.ConfigureTestServices += SetRestApiOptions;

        SqlMapper.AddTypeHandler<IHDPrivateKey>(new HDPrivateKeyTypeHandler(Algorithm));
        SqlMapper.AddTypeHandler<IHDPublicKey>(new HDPublicKeyTypeHandler(Algorithm));
    }

    private void SetRestApiOptions(IServiceCollection services) =>
        services.AddSingleton<IOptions<RestApiOptions>>(
            new OptionsWrapper<RestApiOptions>(new RestApiOptions
            { BasePath = _basePath }));

    public new void Dispose()
    {
        base.Dispose();
        _grpcFixture.ConfigureTestServices -= SetRestApiOptions;
    }

    [Fact]
    public async Task open_api_specification_paths_starts_with_base_path()
    {
        var httpClient = _grpcFixture.CreateHttpClient();
        var specificationResponse = await httpClient.GetAsync("swagger/v1/swagger.json");
        var specification = await specificationResponse.Content.ReadAsStringAsync();
        specification.Should().Contain($"{_basePath}/v1/certificates");
    }

    [Fact]
    public async Task api_returns_ok_when_base_path_is_correct()
    {
        var httpClient = CreateAuthenticatedHttpClient(_fixture.Create<string>(), _fixture.Create<string>());

        var result = await httpClient.GetAsync($"{_basePath}/v1/certificates");

        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task api_returns_not_found_when_base_path_is_incorrect()
    {
        var httpClient = CreateAuthenticatedHttpClient(_fixture.Create<string>(), _fixture.Create<string>());

        PathString wrongBasePath = "/api";

        var result = await httpClient.GetAsync($"{wrongBasePath}/v1/certificates");

        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
