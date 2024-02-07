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
        TestServerFixture<Startup> serverFixture,
        PostgresDatabaseFixture dbFixture,
        InMemoryFixture inMemoryFixture,
        JwtTokenIssuerFixture jwtTokenIssuerFixture,
        ITestOutputHelper outputHelper)
        : base(
            serverFixture,
            dbFixture,
            inMemoryFixture,
            jwtTokenIssuerFixture,
            outputHelper,
            null)
    {
        serverFixture.ConfigureTestServices += SetRestApiOptions;
    }

    private void SetRestApiOptions(IServiceCollection services) =>
        services.AddSingleton<IOptions<RestApiOptions>>(
            new OptionsWrapper<RestApiOptions>(new RestApiOptions { PathBase = _basePath }));

    public new void Dispose()
    {
        base.Dispose();
        _serverFixture.ConfigureTestServices -= SetRestApiOptions;
    }

    [Fact]
    public async Task open_api_specification_paths_starts_with_base_path()
    {
        var httpClient = _serverFixture.CreateHttpClient();
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
