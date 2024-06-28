using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.IntegrationTests.TestExtensions;
using ProjectOrigin.WalletSystem.Server.CommandHandlers;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Options;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Services.REST.v1;
using Xunit;
using TimeAggregate = ProjectOrigin.WalletSystem.Server.Services.REST.v1.TimeAggregate;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class ClaimsControllerTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly Fixture _fixture;
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptions<ServiceOptions> _options;

    public ClaimsControllerTests(PostgresDatabaseFixture postgresDatabaseFixture)
    {
        _fixture = new Fixture();
        _dbFixture = postgresDatabaseFixture;
        _unitOfWork = _dbFixture.CreateUnitOfWork();

        _options = Options.Create(new ServiceOptions
        {
            EndpointAddress = new Uri("https://example.com"),
            PathBase = new PathString("/foo-bar-baz"),
        });
    }

    [Fact]
    public async Task Verify_Unauthorized()
    {
        // Arrange
        var controller = new ClaimsController();

        // Act
        var result = await controller.GetClaims(
            _unitOfWork,
            new GetClaimsQueryParameters());

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Test_ClaimsCursor()
    {
        // Arrange
        var issuestartDate = new DateTimeOffset(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var issueEndDate = new DateTimeOffset(2020, 6, 1, 23, 0, 0, TimeSpan.Zero);

        var subject = _fixture.Create<string>();
        var controller = new ClaimsController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var wallet = await _dbFixture.CreateWallet(subject);
        var endpoint = await _dbFixture.CreateWalletEndpoint(wallet);

        await CreateClaims(issuestartDate, issueEndDate, endpoint, 100);
        var queryUpdatedSince = DateTimeOffset.UtcNow.AddMicroseconds(-2000).ToUnixTimeSeconds();
        // Act
        var result = await controller.GetClaimsCursor(
            _unitOfWork,   new GetClaimsQueryParametersCursor()
                { UpdatedSince = queryUpdatedSince }
        );

        // Assert
        result.Value.Should().NotBeNull();
        var resultList = result.Value!.Result;

        resultList.Should().BeInAscendingOrder(x => x.UpdatedAt);
        var updatedAt = DateTimeOffset.FromUnixTimeSeconds(resultList.Last().UpdatedAt);
        updatedAt.Should().BeOnOrAfter(DateTimeOffset.FromUnixTimeSeconds(queryUpdatedSince));
    }

    [Theory]
    [InlineData("Europe/Copenhagen", new long[] { 1000, 2400, 1400 })]
    [InlineData("Europe/London", new long[] { 1100, 2400, 1300 })]
    [InlineData("America/Toronto", new long[] { 1600, 2400, 800 })]
    public async Task Test_AggregateClaims(string timezone, long[] values)
    {
        // Arrange
        var issuestartDate = new DateTimeOffset(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var issueEndDate = new DateTimeOffset(2020, 6, 30, 12, 0, 0, TimeSpan.Zero);
        var queryStartDate = new DateTimeOffset(2020, 6, 8, 12, 0, 0, TimeSpan.Zero);
        var queryEndDate = new DateTimeOffset(2020, 6, 10, 12, 0, 0, TimeSpan.Zero);

        var subject = _fixture.Create<string>();
        var controller = new ClaimsController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var wallet = await _dbFixture.CreateWallet(subject);
        var endpoint = await _dbFixture.CreateWalletEndpoint(wallet);

        await CreateClaims(issuestartDate, issueEndDate, endpoint);

        // Act
        var result = await controller.AggregateClaims(
            _unitOfWork,
            new AggregateClaimsQueryParameters
            {
                TimeAggregate = TimeAggregate.Day,
                TimeZone = timezone,
                Start = queryStartDate.ToUnixTimeSeconds(),
                End = queryEndDate.ToUnixTimeSeconds()
            });

        // Assert
        result.Value.Should().NotBeNull();
        var resultList = result.Value!.Result;

        resultList.Should().HaveCount(3);
        resultList.Select(x => x.Quantity).Should().ContainInOrder(values);
    }

    private async Task CreateClaims(DateTimeOffset issuestartDate, DateTimeOffset issueEndDate, WalletEndpoint endpoint, int delay = 0)
    {
        for (DateTimeOffset i = issuestartDate; i < issueEndDate; i = i.AddHours(1))
        {
            var prodCert = await _dbFixture.CreateCertificate(
                Guid.NewGuid(),
                _fixture.Create<string>(),
                Server.Models.GranularCertificateType.Production,
                start: i,
                end: i.AddHours(1));
            var prodSlice =
                await _dbFixture.CreateSlice(endpoint, prodCert, new PedersenCommitment.SecretCommitmentInfo(100));

            var consCert = await _dbFixture.CreateCertificate(
                Guid.NewGuid(),
                _fixture.Create<string>(),
                Server.Models.GranularCertificateType.Consumption,
                start: i,
                end: i.AddHours(1));
            var consSlice =
                await _dbFixture.CreateSlice(endpoint, consCert, new PedersenCommitment.SecretCommitmentInfo(100));

            await _dbFixture.CreateClaim(prodSlice, consSlice, Server.Models.ClaimState.Claimed);
            await Task.Delay(delay);
        }
    }

    [Fact]
    public async Task AggregateClaims_Invalid_TimeZone()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new ClaimsController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.AggregateClaims(
            _unitOfWork,
            new AggregateClaimsQueryParameters
            {
                TimeAggregate = TimeAggregate.Day,
                TimeZone = "invalid-time-zone"
            });

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ClaimCertificate_Unauthorized()
    {
        // Arrange
        var controller = new ClaimsController();

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x => { })
            .BuildServiceProvider(true);

        // Act
        var result = await controller.ClaimCertificate(
            provider.GetRequiredService<ITestHarness>().Bus,
            _unitOfWork,
            _options,
            _fixture.Create<ClaimRequest>());

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async void ClaimCertificate_PublishesCommand()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var request = _fixture.Create<ClaimRequest>();

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x => { })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();
        var controller = new ClaimsController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.ClaimCertificate(
            harness.Bus,
            _unitOfWork,
            _options,
            request);

        // Assert
        result.Result.Should().BeOfType<AcceptedResult>();

        var acceptedResult = result.Result as AcceptedResult;
        acceptedResult.Should().NotBeNull();
        acceptedResult!.Location.Should().Contain(_options.Value.PathBase + "/v1/request-status/");

        var response = acceptedResult.Value as ClaimResponse;
        response.Should().NotBeNull();
        var sentMessage = harness.Published.Select<ClaimCertificateCommand>().Should().ContainSingle();
        var sentCommand = sentMessage.Which.Context.Message;

        sentCommand.ClaimId.Should().Be(response!.ClaimRequestId);
        sentCommand.Owner.Should().Be(subject);
        sentCommand.ConsumptionRegistry.Should().Be(request.ConsumptionCertificateId.Registry);
        sentCommand.ConsumptionCertificateId.Should().Be(request.ConsumptionCertificateId.StreamId);
        sentCommand.ProductionRegistry.Should().Be(request.ProductionCertificateId.Registry);
        sentCommand.ProductionCertificateId.Should().Be(request.ProductionCertificateId.StreamId);
        sentCommand.Quantity.Should().Be(request.Quantity);
    }

    private static ControllerContext CreateContextWithUser(string subject)
    {
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new System.Security.Claims.Claim[]
                {
                    new(ClaimTypes.NameIdentifier, subject),
                })),
            }
        };
    }
}
