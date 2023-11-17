using System;
using System.Buffers;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.IntegrationTests.TestExtensions;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Services.REST.v1;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class WalletControllerTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly Fixture _fixture;
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly IUnitOfWork _unitOfWork;

    public WalletControllerTests(PostgresDatabaseFixture postgresDatabaseFixture)
    {
        _fixture = new Fixture();
        _dbFixture = postgresDatabaseFixture;
        _unitOfWork = _dbFixture.CreateUnitOfWork();
    }

    [Fact]
    public async Task Verify_Unauthorized()
    {
        // Arrange
        var controller = new WalletController();

        // Act
        var result = await controller.GetCertificates(
            _unitOfWork,
            null,
            null);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
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
        var controller = new WalletController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var wallet = await _dbFixture.CreateWallet(subject);
        var endpoint = await _dbFixture.CreateWalletEndpoint(wallet);

        for (DateTimeOffset i = issuestartDate; i < issueEndDate; i = i.AddHours(1))
        {
            var prodCert = await _dbFixture.CreateCertificate(
                Guid.NewGuid(),
                _fixture.Create<string>(),
                Server.Models.GranularCertificateType.Production,
                start: i,
                end: i.AddHours(1));
            var prodSlice = await _dbFixture.CreateSlice(endpoint, prodCert, new PedersenCommitment.SecretCommitmentInfo(100));

            var consCert = await _dbFixture.CreateCertificate(
                Guid.NewGuid(),
                _fixture.Create<string>(),
                Server.Models.GranularCertificateType.Consumption,
                start: i,
                end: i.AddHours(1));
            var consSlice = await _dbFixture.CreateSlice(endpoint, consCert, new PedersenCommitment.SecretCommitmentInfo(10));
        }

        // Act
        var result = await controller.AggregateCertificates(
            _unitOfWork,
            TimeAggregate.Day,
            timezone,
            queryStartDate.ToUnixTimeSeconds(),
            queryEndDate.ToUnixTimeSeconds(),
            type);

        // Assert
        result.Value.Should().NotBeNull();
        var resultList = result.Value!.Result;

        resultList.Should().HaveCount(3);
        resultList.Select(x => x.Quantity).Should().ContainInOrder(values);
    }

    [Fact]
    public async Task AggregateCertificates_Invalid_TimeZone()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.AggregateCertificates(
            _unitOfWork,
            TimeAggregate.Day,
            "invalid-time-zone",
            null,
            null,
            null);

        // Assert
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
        var controller = new WalletController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var wallet = await _dbFixture.CreateWallet(subject);
        var endpoint = await _dbFixture.CreateWalletEndpoint(wallet);

        for (DateTimeOffset i = issuestartDate; i < issueEndDate; i = i.AddHours(1))
        {
            var prodCert = await _dbFixture.CreateCertificate(
                Guid.NewGuid(),
                _fixture.Create<string>(),
                Server.Models.GranularCertificateType.Production,
                start: i,
                end: i.AddHours(1));
            var prodSlice = await _dbFixture.CreateSlice(endpoint, prodCert, new PedersenCommitment.SecretCommitmentInfo(100));

            var consCert = await _dbFixture.CreateCertificate(
                Guid.NewGuid(),
                _fixture.Create<string>(),
                Server.Models.GranularCertificateType.Consumption,
                start: i,
                end: i.AddHours(1));
            var consSlice = await _dbFixture.CreateSlice(endpoint, consCert, new PedersenCommitment.SecretCommitmentInfo(100));

            await _dbFixture.CreateClaim(prodSlice, consSlice, Server.Models.ClaimState.Claimed);
        }

        // Act
        var result = await controller.AggregateClaims(
            _unitOfWork,
            TimeAggregate.Day,
            timezone,
            queryStartDate.ToUnixTimeSeconds(),
            queryEndDate.ToUnixTimeSeconds());

        // Assert
        result.Value.Should().NotBeNull();
        var resultList = result.Value!.Result;

        resultList.Should().HaveCount(3);
        resultList.Select(x => x.Quantity).Should().ContainInOrder(values);
    }

    [Fact]
    public async Task AggregateClaims_Invalid_TimeZone()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.AggregateClaims(
            _unitOfWork,
            TimeAggregate.Day,
            "invalid-time-zone",
            null,
            null);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Test_GetTransfers()
    {
        // Arrange
        var issuestartDate = new DateTimeOffset(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var issueEndDate = new DateTimeOffset(2020, 6, 30, 12, 0, 0, TimeSpan.Zero);
        var queryStartDate = new DateTimeOffset(2020, 6, 8, 12, 0, 0, TimeSpan.Zero);
        var queryEndDate = new DateTimeOffset(2020, 6, 10, 12, 0, 0, TimeSpan.Zero);

        var subject = _fixture.Create<string>();
        var controller = new WalletController
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
            queryStartDate.ToUnixTimeSeconds(),
            queryEndDate.ToUnixTimeSeconds());

        // Assert
        result.Value.Should().NotBeNull();
        var resultList = result.Value!.Result;

        resultList.Should().HaveCount(48);
    }

    [Theory]
    [InlineData("Europe/Copenhagen", new long[] { 1000, 2400, 1400 })]
    [InlineData("Europe/London", new long[] { 1100, 2400, 1300 })]
    [InlineData("America/Toronto", new long[] { 1600, 2400, 800 })]
    public async Task Test_AggregateTransfers(string timezone, long[] values)
    {
        // Arrange
        var issuestartDate = new DateTimeOffset(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var issueEndDate = new DateTimeOffset(2020, 6, 30, 12, 0, 0, TimeSpan.Zero);
        var queryStartDate = new DateTimeOffset(2020, 6, 8, 12, 0, 0, TimeSpan.Zero);
        var queryEndDate = new DateTimeOffset(2020, 6, 10, 12, 0, 0, TimeSpan.Zero);

        var subject = _fixture.Create<string>();
        var controller = new WalletController
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
            TimeAggregate.Day,
            timezone,
            queryStartDate.ToUnixTimeSeconds(),
            queryEndDate.ToUnixTimeSeconds());

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
        var controller = new WalletController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.AggregateTransfers(
            _unitOfWork,
            TimeAggregate.Day,
            "invalid-time-zone",
            null,
            null);

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
