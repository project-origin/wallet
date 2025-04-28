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
using MsOptions = Microsoft.Extensions.Options;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using ProjectOrigin.Vault.Tests.TestExtensions;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Options;
using ProjectOrigin.Vault.Services.REST.v1;
using Xunit;

namespace ProjectOrigin.Vault.Tests;

public class TransfersControllerTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly Fixture _fixture;
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MsOptions.IOptions<ServiceOptions> _options;
    private ITransferMetrics _transferMetrics;

    public TransfersControllerTests(PostgresDatabaseFixture postgresDatabaseFixture)
    {
        _fixture = new Fixture();
        _dbFixture = postgresDatabaseFixture;
        _unitOfWork = _dbFixture.CreateUnitOfWork();
        _transferMetrics = Substitute.For<ITransferMetrics>();

        _options = MsOptions.Options.Create(new ServiceOptions
        {
            EndpointAddress = new Uri("https://example.com"),
            PathBase = new PathString("/foo-bar-baz"),
        });
    }

    [Fact]
    public async Task Verify_Unauthorized()
    {
        _transferMetrics = Substitute.For<ITransferMetrics>();

        // Arrange
        var controller = new TransfersController(_transferMetrics);

        // Act
        var result = await controller.GetTransfers(
            _unitOfWork,
            new GetTransfersQueryParameters());

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetTransfersCursor()
    {
        // Arrange
        var issuestartDate = new DateTimeOffset(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var issueEndDate = new DateTimeOffset(2020, 6, 30, 12, 0, 0, TimeSpan.Zero);
        var queryStartDate = new DateTimeOffset(2020, 6, 8, 12, 0, 0, TimeSpan.Zero);
        var queryEndDate = new DateTimeOffset(2020, 6, 10, 12, 0, 0, TimeSpan.Zero);

        var subject = _fixture.Create<string>();
        await _dbFixture.CreateWallet(subject);
        var controller = new TransfersController(_transferMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var externalEndpoint = await _dbFixture.CreateExternalEndpoint(subject);

        for (DateTimeOffset i = issuestartDate; i < issueEndDate; i = i.AddHours(1))
        {
            var certificate = await _dbFixture.CreateCertificate(
                Guid.NewGuid(),
                _fixture.Create<string>(),
                Models.GranularCertificateType.Production,
                start: i,
                end: i.AddHours(1));
            await _dbFixture.CreateTransferredSlice(externalEndpoint, certificate, new PedersenCommitment.SecretCommitmentInfo(100));
        }

        // Act
        var result = await controller.GetTransfersCursor(
            _unitOfWork,
            new GetTransfersQueryParametersCursor()
            {
                Start = queryStartDate.ToUnixTimeSeconds(),
                End = queryEndDate.ToUnixTimeSeconds(),
                UpdatedSince = DateTimeOffset.UtcNow.AddSeconds(-100).ToUnixTimeSeconds()
            });

        // Assert
        result.Value.Should().NotBeNull();
        var resultList = result.Value!.Result;

        resultList.Should().HaveCount(48);
    }

    [Fact]
    public async Task GetTransfers()
    {
        // Arrange
        var issuestartDate = new DateTimeOffset(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var issueEndDate = new DateTimeOffset(2020, 6, 30, 12, 0, 0, TimeSpan.Zero);
        var queryStartDate = new DateTimeOffset(2020, 6, 8, 12, 0, 0, TimeSpan.Zero);
        var queryEndDate = new DateTimeOffset(2020, 6, 10, 12, 0, 0, TimeSpan.Zero);

        var subject = _fixture.Create<string>();
        var controller = new TransfersController(_transferMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        await _dbFixture.CreateWallet(subject);
        var externalEndpoint = await _dbFixture.CreateExternalEndpoint(subject);

        for (DateTimeOffset i = issuestartDate; i < issueEndDate; i = i.AddHours(1))
        {
            var certificate = await _dbFixture.CreateCertificate(
                Guid.NewGuid(),
                _fixture.Create<string>(),
                Models.GranularCertificateType.Production,
                start: i,
                end: i.AddHours(1));
            await _dbFixture.CreateTransferredSlice(externalEndpoint, certificate, new PedersenCommitment.SecretCommitmentInfo(100));
        }

        // Act
        var result = await controller.GetTransfers(
            _unitOfWork,
            new GetTransfersQueryParameters
            {
                Start = queryStartDate.ToUnixTimeSeconds(),
                End = queryEndDate.ToUnixTimeSeconds(),
            });

        // Assert
        result.Value.Should().NotBeNull();
        var resultList = result.Value!.Result;

        resultList.Should().HaveCount(48);
    }

    [Theory]
    [InlineData("Europe/Copenhagen", new long[] { 1000, 2400, 1400 })]
    [InlineData("Europe/London", new long[] { 1100, 2400, 1300 })]
    [InlineData("America/Toronto", new long[] { 1600, 2400, 800 })]
    public async Task AggregateTransfers(string timezone, long[] values)
    {
        // Arrange
        var issuestartDate = new DateTimeOffset(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var issueEndDate = new DateTimeOffset(2020, 6, 30, 12, 0, 0, TimeSpan.Zero);
        var queryStartDate = new DateTimeOffset(2020, 6, 8, 12, 0, 0, TimeSpan.Zero);
        var queryEndDate = new DateTimeOffset(2020, 6, 10, 12, 0, 0, TimeSpan.Zero);

        var subject = _fixture.Create<string>();
        var controller = new TransfersController(_transferMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        await _dbFixture.CreateWallet(subject);
        var externalEndpoint = await _dbFixture.CreateExternalEndpoint(subject);

        for (DateTimeOffset i = issuestartDate; i < issueEndDate; i = i.AddHours(1))
        {
            var certificate = await _dbFixture.CreateCertificate(
                Guid.NewGuid(),
                _fixture.Create<string>(),
                Models.GranularCertificateType.Production,
                start: i,
                end: i.AddHours(1));
            await _dbFixture.CreateTransferredSlice(externalEndpoint, certificate, new PedersenCommitment.SecretCommitmentInfo(100));
        }

        // Act
        var result = await controller.AggregateTransfers(
            _unitOfWork,
            new AggregateTransfersQueryParameters
            {
                TimeAggregate = TimeAggregate.Day,
                TimeZone = timezone,
                Start = queryStartDate.ToUnixTimeSeconds(),
                End = queryEndDate.ToUnixTimeSeconds(),
            });

        // Assert
        result.Value.Should().NotBeNull();
        var resultList = result.Value!.Result;

        resultList.Should().HaveCount(3);
        resultList.Select(x => x.Quantity).Should().ContainInOrder(values);
    }

    [Fact]
    public async Task AggregateTransfers_Invalid_TimeZone()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new TransfersController(_transferMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.AggregateTransfers(
            _unitOfWork,
            new AggregateTransfersQueryParameters
            {
                TimeAggregate = TimeAggregate.Day,
                TimeZone = "invalid-time-zone",
            });

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TransferCertificate_Unauthorized()
    {
        // Arrange
        var controller = new TransfersController(_transferMetrics);

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
            })
            .BuildServiceProvider(true);

        // Act
        var result = await controller.TransferCertificate(
            _unitOfWork,
            _options,
            _fixture.Create<TransferRequest>());

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task IfReceivedTransferRequestButNoSuccessResponse_DoesNotIncrementCounters()
    {
        // Arrange
        var controller = new TransfersController(_transferMetrics);

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
            })
            .BuildServiceProvider(true);

        // Act
        var result = await controller.TransferCertificate(
            _unitOfWork,
            _options,
            _fixture.Create<TransferRequest>());

        // Assert
        _transferMetrics.DidNotReceive().IncrementTransferIntents();
        _transferMetrics.DidNotReceive().IncrementCompleted();
    }

    [Fact]
    public async Task TransferCertificate_TransferCommand_AcceptedResult()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var request = _fixture.Create<TransferRequest>();
        await _dbFixture.CreateWallet(subject);

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();
        var controller = new TransfersController(_transferMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.TransferCertificate(
            _unitOfWork,
            _options,
            request);

        // Assert
        result.Result.Should().BeOfType<AcceptedResult>();

        var acceptedResult = result.Result as AcceptedResult;
        acceptedResult.Should().NotBeNull();
        acceptedResult!.Location.Should().Contain(_options.Value.PathBase + "/v1/request-status/");

        var response = acceptedResult.Value as TransferResponse;
        response.Should().NotBeNull();
    }

    [Fact]
    public async Task SuccessfulTransferIncrementsIntentCounter()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var request = _fixture.Create<TransferRequest>();
        await _dbFixture.CreateWallet(subject);

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();
        var controller = new TransfersController(_transferMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.TransferCertificate(
            _unitOfWork,
            _options,
            request);

        result.Result.Should().BeOfType<AcceptedResult>();

        // Assert
        _transferMetrics.Received(1).IncrementTransferIntents();
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
