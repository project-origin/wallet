using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using ProjectOrigin.Vault.Tests.TestExtensions;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Services.REST.v1;
using Xunit;
using TimeAggregate = ProjectOrigin.Vault.Services.REST.v1.TimeAggregate;

namespace ProjectOrigin.Vault.Tests;

public class CertificatesControllerTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly Fixture _fixture;
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly IUnitOfWork _unitOfWork;

    public CertificatesControllerTests(PostgresDatabaseFixture postgresDatabaseFixture)
    {
        _fixture = new Fixture();
        _dbFixture = postgresDatabaseFixture;
        _unitOfWork = _dbFixture.CreateUnitOfWork();
    }

    [Fact]
    public async Task Verify_Unauthorized()
    {
        // Arrange
        var controller = new CertificatesController();

        // Act
        var result = await controller.GetCertificates(_unitOfWork, new GetCertificatesQueryParameters());

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetCertificate_WhenNoWallet_NotFound()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var registry = Guid.NewGuid().ToString();
        var streamId = Guid.NewGuid();
        var controller = new CertificatesController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.GetCertificate(_unitOfWork, registry, streamId);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetCertificate_WhenWalletIsDisabled_BadRequest()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var registry = Guid.NewGuid().ToString();
        var streamId = Guid.NewGuid();
        var wallet = await _dbFixture.CreateWallet(subject);
        await _dbFixture.DisableWallet(wallet.Id);
        var controller = new CertificatesController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.GetCertificate(_unitOfWork, registry, streamId);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetCertificatesCursor_WhenNoWallet_NotFound()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new CertificatesController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.GetCertificatesCursor(_unitOfWork, new GetCertificatesQueryParametersCursor());

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetCertificatesCursor_WhenWalletIsDisabled_BadRequest()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var wallet = await _dbFixture.CreateWallet(subject);
        await _dbFixture.DisableWallet(wallet.Id);
        var controller = new CertificatesController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.GetCertificatesCursor(_unitOfWork, new GetCertificatesQueryParametersCursor());

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetCertificates_WhenNoWallet_NotFound()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new CertificatesController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.GetCertificates(_unitOfWork, new GetCertificatesQueryParameters());

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetCertificates_WhenWalletIsDisabled_BadRequest()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var wallet = await _dbFixture.CreateWallet(subject);
        await _dbFixture.DisableWallet(wallet.Id);
        var controller = new CertificatesController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.GetCertificates(_unitOfWork, new GetCertificatesQueryParameters());

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AggregateCertificates_WhenNoWallet_NotFound()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new CertificatesController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var queryStartDate = new DateTimeOffset(2020, 6, 8, 12, 0, 0, TimeSpan.Zero);
        var queryEndDate = new DateTimeOffset(2020, 6, 10, 12, 0, 0, TimeSpan.Zero);
        var result = await controller.AggregateCertificates(_unitOfWork,
            new AggregateCertificatesQueryParameters
            {
                TimeAggregate = TimeAggregate.Day,
                TimeZone = "Europe/Copenhagen",
                Start = queryStartDate.ToUnixTimeSeconds(),
                End = queryEndDate.ToUnixTimeSeconds(),
                Type = CertificateType.Production
            });

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AggregateCertificates_WhenWalletIsDisabled_BadRequest()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var wallet = await _dbFixture.CreateWallet(subject);
        await _dbFixture.DisableWallet(wallet.Id);
        var controller = new CertificatesController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var queryStartDate = new DateTimeOffset(2020, 6, 8, 12, 0, 0, TimeSpan.Zero);
        var queryEndDate = new DateTimeOffset(2020, 6, 10, 12, 0, 0, TimeSpan.Zero);
        var result = await controller.AggregateCertificates(_unitOfWork,
            new AggregateCertificatesQueryParameters
            {
                TimeAggregate = TimeAggregate.Day,
                TimeZone = "Europe/Copenhagen",
                Start = queryStartDate.ToUnixTimeSeconds(),
                End = queryEndDate.ToUnixTimeSeconds(),
                Type = CertificateType.Production
            });

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Theory]
    [InlineData("Europe/Copenhagen", new long[] { 1000, 2400, 1400 }, CertificateType.Production)]
    [InlineData("Europe/London", new long[] { 110, 240, 130 }, CertificateType.Consumption)]
    [InlineData("America/Toronto", new long[] { 1600, 2400, 800 }, CertificateType.Production)]
    public async Task Test_AggregateCertificates(string timezone, long[] values, CertificateType type)
    {
        // Arrange
        var issuestartDate = new DateTimeOffset(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var issueEndDate = new DateTimeOffset(2020, 6, 30, 12, 0, 0, TimeSpan.Zero);
        var queryStartDate = new DateTimeOffset(2020, 6, 8, 12, 0, 0, TimeSpan.Zero);
        var queryEndDate = new DateTimeOffset(2020, 6, 10, 12, 0, 0, TimeSpan.Zero);

        var subject = _fixture.Create<string>();
        var controller = new CertificatesController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var wallet = await _dbFixture.CreateWallet(subject);
        var endpoint = await _dbFixture.CreateWalletEndpoint(wallet);

        await CreateCertificates(issuestartDate, issueEndDate, endpoint);


        // Act
        var result = await controller.AggregateCertificates(
            _unitOfWork,
            new AggregateCertificatesQueryParameters
            {
                TimeAggregate = TimeAggregate.Day,
                TimeZone = timezone,
                Start = queryStartDate.ToUnixTimeSeconds(),
                End = queryEndDate.ToUnixTimeSeconds(),
                Type = type
            });

        // Assert
        result.Value.Should().NotBeNull();
        var resultList = result.Value!.Result;

        resultList.Should().HaveCount(3);
        resultList.Select(x => x.Quantity).Should().ContainInOrder(values);
    }

    [Fact]
    public async Task Certificates_Cursor()
    {
        var issuestartDate = new DateTimeOffset(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var issueEndDate = new DateTimeOffset(2020, 6, 30, 12, 0, 0, TimeSpan.Zero);
        var queryStartDate = new DateTimeOffset(2020, 6, 8, 12, 0, 0, TimeSpan.Zero);
        var queryEndDate = new DateTimeOffset(2020, 6, 10, 12, 0, 0, TimeSpan.Zero);

        var subject = _fixture.Create<string>();
        var controller = new CertificatesController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var wallet = await _dbFixture.CreateWallet(subject);
        var endpoint = await _dbFixture.CreateWalletEndpoint(wallet);

        await CreateCertificates(issuestartDate, issueEndDate, endpoint);

        var result = await controller.GetCertificatesCursor(
            _unitOfWork,
            new GetCertificatesQueryParametersCursor
            {
                Limit = 10,
                Start = queryStartDate.ToUnixTimeSeconds(),
                End = queryEndDate.ToUnixTimeSeconds(),
                UpdatedSince = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds()
            });
        result.Value.Should().NotBeNull();
        var resultList = result.Value!.Result;

        resultList.Should().HaveCount(10);
    }

    private async Task CreateCertificates(DateTimeOffset issuestartDate, DateTimeOffset issueEndDate,
        WalletEndpoint endpoint)
    {
        for (DateTimeOffset i = issuestartDate; i < issueEndDate; i = i.AddHours(1))
        {
            var prodCert = await _dbFixture.CreateCertificate(
                Guid.NewGuid(),
                _fixture.Create<string>(),
                Models.GranularCertificateType.Production,
                start: i,
                end: i.AddHours(1));
            await _dbFixture.CreateSlice(endpoint, prodCert, new PedersenCommitment.SecretCommitmentInfo(100));

            var consCert = await _dbFixture.CreateCertificate(
                Guid.NewGuid(),
                _fixture.Create<string>(),
                Models.GranularCertificateType.Consumption,
                start: i,
                end: i.AddHours(1));
            await _dbFixture.CreateSlice(endpoint, consCert, new PedersenCommitment.SecretCommitmentInfo(10));
        }
    }

    [Fact]
    public async Task AggregateCertificates_Invalid_TimeZone()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new CertificatesController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.AggregateCertificates(
            _unitOfWork,
            new AggregateCertificatesQueryParameters
            {
                TimeAggregate = TimeAggregate.Day,
                TimeZone = "invalid-time-zone",
            });

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
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
