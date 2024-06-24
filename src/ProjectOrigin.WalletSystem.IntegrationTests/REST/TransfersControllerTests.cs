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
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.IntegrationTests.TestExtensions;
using ProjectOrigin.WalletSystem.Server.CommandHandlers;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Services.REST.v1;
using Xunit;
using RequestStatus = ProjectOrigin.WalletSystem.Server.Services.REST.v1.RequestStatus;
using TimeAggregate = ProjectOrigin.WalletSystem.Server.Services.REST.v1.TimeAggregate;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class TransfersControllerTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly Fixture _fixture;
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly IUnitOfWork _unitOfWork;

    public TransfersControllerTests(PostgresDatabaseFixture postgresDatabaseFixture)
    {
        _fixture = new Fixture();
        _dbFixture = postgresDatabaseFixture;
        _unitOfWork = _dbFixture.CreateUnitOfWork();
    }

    [Fact]
    public async Task Verify_Unauthorized()
    {
        // Arrange
        var controller = new TransfersController();

        // Act
        var result = await controller.GetTransfers(
            _unitOfWork,
            new GetTransfersQueryParameters());

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
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
        var controller = new TransfersController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var externalEndpoint = await _dbFixture.CreateExternalEndpoint(subject);

        for (DateTimeOffset i = issuestartDate; i < issueEndDate; i = i.AddHours(1))
        {
            var certificate = await _dbFixture.CreateCertificate(
                Guid.NewGuid(),
                _fixture.Create<string>(),
                Server.Models.GranularCertificateType.Production,
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
        var controller = new TransfersController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var externalEndpoint = await _dbFixture.CreateExternalEndpoint(subject);

        for (DateTimeOffset i = issuestartDate; i < issueEndDate; i = i.AddHours(1))
        {
            var certificate = await _dbFixture.CreateCertificate(
                Guid.NewGuid(),
                _fixture.Create<string>(),
                Server.Models.GranularCertificateType.Production,
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
        var controller = new TransfersController
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
        var controller = new TransfersController();

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
            })
            .BuildServiceProvider(true);

        // Act
        var result = await controller.TransferCertificate(
            provider.GetRequiredService<ITestHarness>().Bus,
            _unitOfWork,
            _fixture.Create<TransferRequest>());

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetTransferStatus_Unauthorized()
    {
        var controller = new TransfersController();

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
            })
            .BuildServiceProvider(true);

        var result = await controller.GetTransferStatus(_unitOfWork, Guid.NewGuid());

        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetTransferStatus_NotFound()
    {
        var subject = _fixture.Create<string>();
        var controller = new TransfersController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
            })
            .BuildServiceProvider(true);

        var result = await controller.GetTransferStatus(_unitOfWork, Guid.NewGuid());

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetTransferStatus()
    {
        var subject = _fixture.Create<string>();
        var controller = new TransfersController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
            })
            .BuildServiceProvider(true);

        var transferRequestId = Guid.NewGuid();
        var status = new Server.Models.RequestStatus
        {
            RequestId = transferRequestId,
            Status = RequestStatusState.Completed
        };

        await _dbFixture.CreateTransferStatus(status);

        var result = await controller.GetTransferStatus(_unitOfWork, transferRequestId);

        var response = (result.Result as OkObjectResult)?.Value as TransferStatusResponse;
        response.Should().NotBeNull();
        response.Status.Should().Be(RequestStatus.Completed);
    }

    [Fact]
    public async void TransferCertificate_SavesStatusAndPublishesCommand()
    {

        // Arrange
        var subject = _fixture.Create<string>();
        var request = _fixture.Create<TransferRequest>();

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();
        var controller = new TransfersController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.TransferCertificate(
            harness.Bus,
            _unitOfWork,
            request);

        // Assert
        result.Result.Should().BeOfType<AcceptedResult>();
        var response = (result.Result as AcceptedResult)?.Value as TransferResponse;
        response.Should().NotBeNull();
        var sentMessage = harness.Published.Select<TransferCertificateCommand>().Should().ContainSingle();
        var sentCommand = sentMessage.Which.Context.Message;

        sentCommand.TransferRequestId.Should().Be(response!.TransferRequestId);
        sentCommand.Owner.Should().Be(subject);
        sentCommand.Registry.Should().Be(request.CertificateId.Registry);
        sentCommand.CertificateId.Should().Be(request.CertificateId.StreamId);
        sentCommand.Quantity.Should().Be(request.Quantity);
        sentCommand.Receiver.Should().Be(request.ReceiverId);

        var statusResult = await controller.GetTransferStatus(_unitOfWork, response.TransferRequestId);

        var statusResponse = (statusResult.Result as OkObjectResult)?.Value as TransferStatusResponse;
        statusResponse.Should().NotBeNull();
        statusResponse.Status.Should().Be(RequestStatus.Pending);
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
