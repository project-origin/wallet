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
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using MsOptions = Microsoft.Extensions.Options;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using ProjectOrigin.Vault.Tests.TestExtensions;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Options;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Services.REST.v1;
using Xunit;
using TimeAggregate = ProjectOrigin.Vault.Services.REST.v1.TimeAggregate;

namespace ProjectOrigin.Vault.Tests;

public class ClaimsControllerTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly Fixture _fixture;
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MsOptions.IOptions<ServiceOptions> _options;
    private readonly IClaimMetrics _claimMetrics;

    public ClaimsControllerTests(PostgresDatabaseFixture postgresDatabaseFixture)
    {
        _fixture = new Fixture();
        _dbFixture = postgresDatabaseFixture;
        _unitOfWork = _dbFixture.CreateUnitOfWork();
        _claimMetrics = Substitute.For<IClaimMetrics>();

        _options = MsOptions.Options.Create(new ServiceOptions
        {
            EndpointAddress = new Uri("https://example.com"),
            PathBase = new PathString("/foo-bar-baz"),
        });
    }

    [Fact]
    public async Task Verify_Unauthorized()
    {
        // Arrange
        var controller = new ClaimsController(_claimMetrics);

        // Act
        var result = await controller.GetClaims(
            _unitOfWork,
            new GetClaimsQueryParameters());

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetClaims_DisabledWallet_BadRequest()
    {
        var subject = _fixture.Create<string>();
        var controller = new ClaimsController(_claimMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };
        var wallet = await _dbFixture.CreateWallet(subject);
        await _dbFixture.DisableWallet(wallet.Id);

        var result = await controller.GetClaims(
            _unitOfWork,
            new GetClaimsQueryParameters());

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Test_ClaimsCursor()
    {
        // Arrange
        var issuestartDate = new DateTimeOffset(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var issueEndDate = new DateTimeOffset(2020, 6, 1, 23, 0, 0, TimeSpan.Zero);

        var subject = _fixture.Create<string>();
        var controller = new ClaimsController(_claimMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var wallet = await _dbFixture.CreateWallet(subject);
        var endpoint = await _dbFixture.CreateWalletEndpoint(wallet);

        await CreateClaims(issuestartDate, issueEndDate, endpoint, 10);
        var queryUpdatedSince = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        // Act
        var result = await controller.GetClaimsCursor(
            _unitOfWork, new GetClaimsQueryParametersCursor()
            { UpdatedSince = queryUpdatedSince }
        );

        // Assert
        result.Value.Should().NotBeNull();
        var resultList = result.Value!.Result;

        resultList.Should().BeInAscendingOrder(x => x.UpdatedAt);
        var updatedAt = DateTimeOffset.FromUnixTimeSeconds(resultList.Last().UpdatedAt);
        updatedAt.Should().BeOnOrAfter(DateTimeOffset.FromUnixTimeSeconds(queryUpdatedSince));
    }

    [Fact]
    public async Task GetClaimsCursor_DisabledWallet_BadRequest()
    {
        var subject = _fixture.Create<string>();
        var controller = new ClaimsController(_claimMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };
        var wallet = await _dbFixture.CreateWallet(subject);
        await _dbFixture.DisableWallet(wallet.Id);

        var result = await controller.GetClaimsCursor(
            _unitOfWork,
            new GetClaimsQueryParametersCursor());

        result.Result.Should().BeOfType<BadRequestObjectResult>();
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
        var controller = new ClaimsController(_claimMetrics)
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
                Models.GranularCertificateType.Production,
                start: i,
                end: i.AddHours(1));
            var prodSlice =
                await _dbFixture.CreateSlice(endpoint, prodCert, new PedersenCommitment.SecretCommitmentInfo(100));

            var consCert = await _dbFixture.CreateCertificate(
                Guid.NewGuid(),
                _fixture.Create<string>(),
                Models.GranularCertificateType.Consumption,
                start: i,
                end: i.AddHours(1));
            var consSlice =
                await _dbFixture.CreateSlice(endpoint, consCert, new PedersenCommitment.SecretCommitmentInfo(100));

            await _dbFixture.CreateClaim(prodSlice, consSlice, Models.ClaimState.Claimed);
            await Task.Delay(delay);
        }
    }

    [Fact]
    public async Task AggregateClaims_Invalid_TimeZone()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new ClaimsController(_claimMetrics)
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
    public async Task AggregateClaims_DisabledWallet_BadRequest()
    {
        var subject = _fixture.Create<string>();
        var controller = new ClaimsController(_claimMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };
        var wallet = await _dbFixture.CreateWallet(subject);
        await _dbFixture.DisableWallet(wallet.Id);

        var result = await controller.AggregateClaims(
            _unitOfWork,
            new AggregateClaimsQueryParameters
            {
                TimeAggregate = TimeAggregate.Day,
                TimeZone = "invalid-time-zone"
            });


        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ClaimCertificate_Unauthorized()
    {
        // Arrange
        var controller = new ClaimsController(_claimMetrics);

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x => { })
            .BuildServiceProvider(true);

        // Act
        var result = await controller.ClaimCertificate(
            _unitOfWork,
            _options,
            _fixture.Create<ClaimRequest>());

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    private async Task<(FederatedStreamId prodCertId, FederatedStreamId conCertId)> CreateCertificates(DateTimeOffset issuerStartDate, DateTimeOffset issuerEndDate, WalletEndpoint walletEndpoint)
    {
        var prodCert = await _dbFixture.CreateCertificate(
            Guid.NewGuid(),
            _fixture.Create<string>(),
            Models.GranularCertificateType.Production,
            start: issuerStartDate,
            end: issuerEndDate.AddHours(1));
        var prodSlice =
            await _dbFixture.CreateSlice(walletEndpoint, prodCert, new PedersenCommitment.SecretCommitmentInfo(100));

        var conCert = await _dbFixture.CreateCertificate(
            Guid.NewGuid(),
            _fixture.Create<string>(),
            Models.GranularCertificateType.Consumption,
            start: issuerStartDate,
            end: issuerEndDate.AddHours(1));
        var consSlice =
            await _dbFixture.CreateSlice(walletEndpoint, conCert, new PedersenCommitment.SecretCommitmentInfo(100));

        return (new FederatedStreamId { Registry = prodCert.RegistryName, StreamId = prodCert.Id },
            new FederatedStreamId { Registry = conCert.RegistryName, StreamId = conCert.Id });
    }

    [Fact]
    public async Task ClaimCertificate_DisabledWallet_BadRequest()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var wallet = await _dbFixture.CreateWallet(subject);
        await _dbFixture.DisableWallet(wallet.Id);
        var endpoint = await _dbFixture.CreateWalletEndpoint(wallet);
        var issuerStartDate = new DateTimeOffset(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);

        var (prodCertId, conCertId) = await CreateCertificates(issuerStartDate, issuerStartDate.AddHours(1), endpoint);

        var request = new ClaimRequest
        {
            ConsumptionCertificateId = prodCertId,
            ProductionCertificateId = conCertId,
            Quantity = 100
        };

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x => { })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();
        var controller = new ClaimsController(_claimMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.ClaimCertificate(
            _unitOfWork,
            _options,
            request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ClaimCertificate_ClaimCommand_AcceptedResult()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var wallet = await _dbFixture.CreateWallet(subject);
        var endpoint = await _dbFixture.CreateWalletEndpoint(wallet);
        var issuerStartDate = new DateTimeOffset(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);

        var (prodCertId, conCertId) = await CreateCertificates(issuerStartDate, issuerStartDate.AddHours(1), endpoint);

        var request = new ClaimRequest
        {
            ConsumptionCertificateId = prodCertId,
            ProductionCertificateId = conCertId,
            Quantity = 100
        };

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x => { })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();
        var controller = new ClaimsController(_claimMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.ClaimCertificate(
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
    }

    [Fact]
    public async Task ClaimCertificate_IncrementsClaimIntentsCounter()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var wallet = await _dbFixture.CreateWallet(subject);
        var endpoint = await _dbFixture.CreateWalletEndpoint(wallet);
        var issuerStartDate = new DateTimeOffset(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);

        var (prodCertId, conCertId) = await CreateCertificates(issuerStartDate, issuerStartDate.AddHours(1), endpoint);

        var request = new ClaimRequest
        {
            ConsumptionCertificateId = prodCertId,
            ProductionCertificateId = conCertId,
            Quantity = 100
        };

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x => { })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();
        var controller = new ClaimsController(_claimMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        await controller.ClaimCertificate(
            _unitOfWork,
            _options,
            request);

        // Assert
        _claimMetrics.Received(1).IncrementClaimIntents();
    }

    [Fact]
    public async Task IfReceivedCreateClaimRequestButNoSuccessResponse_DoNotIncrementIntentsCounter()
    {
        // Arrange
        var controller = new ClaimsController(_claimMetrics);

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x => { })
            .BuildServiceProvider(true);

        // Act
        var result = await controller.ClaimCertificate(
            _unitOfWork,
            _options,
            _fixture.Create<ClaimRequest>());

        // Assert
        _claimMetrics.Received(0).IncrementClaimIntents();
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
