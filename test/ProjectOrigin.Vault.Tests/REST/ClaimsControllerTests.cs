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
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Repositories;
using ProjectOrigin.Vault.Services.REST.v1;
using Xunit;
using TimeAggregate = ProjectOrigin.Vault.Services.REST.v1.TimeAggregate;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.Vault.Tests;

public class ClaimsControllerTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly Fixture _fixture;
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MsOptions.IOptions<ServiceOptions> _options;
    private readonly IClaimMetrics _claimMetrics;
    private readonly IHDAlgorithm _algorithm;

    public ClaimsControllerTests(PostgresDatabaseFixture postgresDatabaseFixture)
    {
        _fixture = new Fixture();
        _dbFixture = postgresDatabaseFixture;
        _unitOfWork = _dbFixture.CreateUnitOfWork();
        _claimMetrics = Substitute.For<IClaimMetrics>();
        _algorithm = new Secp256k1Algorithm();

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
    public async Task GetClaims_NoWallet_NotFound()
    {
        var subject = _fixture.Create<string>();
        var controller = new ClaimsController(_claimMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var result = await controller.GetClaims(
            _unitOfWork,
            new GetClaimsQueryParameters());

        result.Result.Should().BeOfType<NotFoundObjectResult>();
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
    public async Task ReserveQuantity_IsCalledForConsumptionAndProduction()
    {
        var subject = _fixture.Create<string>();
        var request = _fixture.Create<ClaimRequest>();
        var unitOfWorkMock = Substitute.For<IUnitOfWork>();
        var certificateRepositoryMock = Substitute.For<ICertificateRepository>();
        var walletRepositoryMock = Substitute.For<IWalletRepository>();

        walletRepositoryMock.GetWallet(Arg.Any<string>()).Returns(new Wallet
        {
            Id = Guid.NewGuid(),
            Owner = subject,
            PrivateKey = _algorithm.GenerateNewPrivateKey()
        });

        certificateRepositoryMock.GetRegisteringAndAvailableQuantity(
                request.ProductionCertificateId.Registry,
                request.ProductionCertificateId.StreamId,
                subject)
            .Returns(request.Quantity + 10);

        certificateRepositoryMock.GetRegisteringAndAvailableQuantity(
                request.ConsumptionCertificateId.Registry,
                request.ConsumptionCertificateId.StreamId,
                subject)
            .Returns(request.Quantity + 10);

        certificateRepositoryMock.GetCertificate(request.ProductionCertificateId.Registry, request.ProductionCertificateId.StreamId)
            .Returns(new Certificate
            {
                Id = Guid.NewGuid(),
                RegistryName = request.ProductionCertificateId.Registry,
                StartDate = DateTimeOffset.UtcNow.AddDays(-1), // Example start date
                EndDate = DateTimeOffset.UtcNow.AddDays(1),   // Example end date
                GridArea = "ExampleGridArea",                // Example grid area
                CertificateType = GranularCertificateType.Production,
                Withdrawn = false
            });
        certificateRepositoryMock.GetCertificate(request.ConsumptionCertificateId.Registry, request.ConsumptionCertificateId.StreamId)
            .Returns(new Certificate
            {
                Id = Guid.NewGuid(),
                RegistryName = request.ConsumptionCertificateId.Registry,
                StartDate = DateTimeOffset.UtcNow.AddDays(-1), // Example start date
                EndDate = DateTimeOffset.UtcNow.AddDays(1),   // Example end date
                GridArea = "ExampleGridArea",                // Example grid area
                CertificateType = GranularCertificateType.Consumption,
                Withdrawn = false
            });

        unitOfWorkMock.CertificateRepository.Returns(certificateRepositoryMock);
        unitOfWorkMock.WalletRepository.Returns(walletRepositoryMock);

        var controller = new ClaimsController(_claimMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        await controller.ClaimCertificate(unitOfWorkMock, _options, request);

        await certificateRepositoryMock.Received(1).ReserveQuantity(
            subject,
            request.ConsumptionCertificateId.Registry,
            request.ConsumptionCertificateId.StreamId,
            request.Quantity);

        await certificateRepositoryMock.Received(1).ReserveQuantity(
            subject,
            request.ProductionCertificateId.Registry,
            request.ProductionCertificateId.StreamId,
            request.Quantity);
    }

    [Fact]
    public async Task QueryClaims_OnlyReturnsClaimsWithinOneHour()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new ClaimsController(_claimMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var wallet = await _dbFixture.CreateWallet(subject);
        var endpoint = await _dbFixture.CreateWalletEndpoint(wallet);
        var baseTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

        // 0-minute diff — included
        {
            var prodCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Production, baseTime, baseTime.AddHours(1));
            var prodSlice = await _dbFixture.CreateSlice(endpoint, prodCert, new PedersenCommitment.SecretCommitmentInfo(100));

            var consCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Consumption, baseTime, baseTime.AddHours(1));
            var consSlice = await _dbFixture.CreateSlice(endpoint, consCert, new PedersenCommitment.SecretCommitmentInfo(100));

            await _dbFixture.CreateClaim(prodSlice, consSlice, Models.ClaimState.Claimed);
        }

        // 59-minute diff — included
        {
            var prodCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Production, baseTime, baseTime.AddHours(1));
            var prodSlice = await _dbFixture.CreateSlice(endpoint, prodCert, new PedersenCommitment.SecretCommitmentInfo(100));

            var consStart = baseTime.AddMinutes(59);
            var consCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Consumption, consStart, consStart.AddHours(1));
            var consSlice = await _dbFixture.CreateSlice(endpoint, consCert, new PedersenCommitment.SecretCommitmentInfo(100));

            await _dbFixture.CreateClaim(prodSlice, consSlice, Models.ClaimState.Claimed);
        }

        // 60-minute diff — filtered out
        {
            var prodCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Production, baseTime, baseTime.AddHours(1));
            var prodSlice = await _dbFixture.CreateSlice(endpoint, prodCert, new PedersenCommitment.SecretCommitmentInfo(100));

            var consStart = baseTime.AddMinutes(60);
            var consCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Consumption, consStart, consStart.AddHours(1));
            var consSlice = await _dbFixture.CreateSlice(endpoint, consCert, new PedersenCommitment.SecretCommitmentInfo(100));

            await _dbFixture.CreateClaim(prodSlice, consSlice, Models.ClaimState.Claimed);
        }

        // 61-minute diff — filtered out
        {
            var prodCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Production, baseTime, baseTime.AddHours(1));
            var prodSlice = await _dbFixture.CreateSlice(endpoint, prodCert, new PedersenCommitment.SecretCommitmentInfo(100));

            var consStart = baseTime.AddMinutes(61);
            var consCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Consumption, consStart, consStart.AddHours(1));
            var consSlice = await _dbFixture.CreateSlice(endpoint, consCert, new PedersenCommitment.SecretCommitmentInfo(100));

            await _dbFixture.CreateClaim(prodSlice, consSlice, Models.ClaimState.Claimed);
        }

        // Act
        var result = await controller.GetClaims(_unitOfWork, new GetClaimsQueryParameters
        {
            Start = baseTime.AddHours(-1).ToUnixTimeSeconds(),
            End = baseTime.AddHours(3).ToUnixTimeSeconds(),
            Limit = 100,
            Skip = 0
        });

        // Assert
        result.Value.Should().NotBeNull();
        var resultList = result.Value!.Result;
        resultList.Should().HaveCount(2);
    }

    [Fact]
    public async Task QueryClaims_ReturnsAllClaims_WhenTimeMatchIsSetToAll()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new ClaimsController(_claimMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var wallet = await _dbFixture.CreateWallet(subject);
        var endpoint = await _dbFixture.CreateWalletEndpoint(wallet);
        var baseTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

        // 0-minute diff — included
        {
            var prodCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Production, baseTime, baseTime.AddHours(1));
            var prodSlice = await _dbFixture.CreateSlice(endpoint, prodCert, new PedersenCommitment.SecretCommitmentInfo(100));

            var consCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Consumption, baseTime, baseTime.AddHours(1));
            var consSlice = await _dbFixture.CreateSlice(endpoint, consCert, new PedersenCommitment.SecretCommitmentInfo(100));

            await _dbFixture.CreateClaim(prodSlice, consSlice, Models.ClaimState.Claimed);
        }

        // 60-minute diff — included
        {
            var prodCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Production, baseTime, baseTime.AddHours(1));
            var prodSlice = await _dbFixture.CreateSlice(endpoint, prodCert, new PedersenCommitment.SecretCommitmentInfo(100));

            var consStart = baseTime.AddMinutes(60);
            var consCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Consumption, consStart, consStart.AddHours(1));
            var consSlice = await _dbFixture.CreateSlice(endpoint, consCert, new PedersenCommitment.SecretCommitmentInfo(100));

            await _dbFixture.CreateClaim(prodSlice, consSlice, Models.ClaimState.Claimed);
        }

        // 61-minute diff — included
        {
            var prodCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Production, baseTime, baseTime.AddHours(1));
            var prodSlice = await _dbFixture.CreateSlice(endpoint, prodCert, new PedersenCommitment.SecretCommitmentInfo(100));

            var consStart = baseTime.AddMinutes(61);
            var consCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Consumption, consStart, consStart.AddHours(1));
            var consSlice = await _dbFixture.CreateSlice(endpoint, consCert, new PedersenCommitment.SecretCommitmentInfo(100));

            await _dbFixture.CreateClaim(prodSlice, consSlice, Models.ClaimState.Claimed);
        }

        // Act
        var result = await controller.GetClaims(_unitOfWork, new GetClaimsQueryParameters
        {
            Start = baseTime.AddHours(-1).ToUnixTimeSeconds(),
            End = baseTime.AddHours(3).ToUnixTimeSeconds(),
            Limit = 100,
            Skip = 0,
            TimeMatch = TimeMatch.All
        });

        // Assert
        result.Value.Should().NotBeNull();
        var resultList = result.Value!.Result;
        resultList.Should().HaveCount(3);
    }

    [Fact]
    public async Task QueryClaimsCursor_OnlyReturnsClaimsWithinOneHour()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new ClaimsController(_claimMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var wallet = await _dbFixture.CreateWallet(subject);
        var endpoint = await _dbFixture.CreateWalletEndpoint(wallet);
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10); // recent claims

        // Included claim: 0-minute diff
        {
            var prod = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Production, baseTime, baseTime.AddHours(1));
            var prodSlice = await _dbFixture.CreateSlice(endpoint, prod, new PedersenCommitment.SecretCommitmentInfo(100));
            var cons = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Consumption, baseTime, baseTime.AddHours(1));
            var consSlice = await _dbFixture.CreateSlice(endpoint, cons, new PedersenCommitment.SecretCommitmentInfo(100));
            await _dbFixture.CreateClaim(prodSlice, consSlice, Models.ClaimState.Claimed);
        }

        // Filtered-out claim: 70-minute diff
        {
            var prod = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Production, baseTime.AddHours(-1), baseTime);
            var prodSlice = await _dbFixture.CreateSlice(endpoint, prod, new PedersenCommitment.SecretCommitmentInfo(100));
            var consStart = baseTime.AddMinutes(10);
            var cons = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Consumption, consStart, consStart.AddHours(1));
            var consSlice = await _dbFixture.CreateSlice(endpoint, cons, new PedersenCommitment.SecretCommitmentInfo(100));
            await _dbFixture.CreateClaim(prodSlice, consSlice, Models.ClaimState.Claimed);
        }

        // Act
        var result = await controller.GetClaimsCursor(
            _unitOfWork,
            new GetClaimsQueryParametersCursor
            {
                UpdatedSince = baseTime.AddMinutes(-15).ToUnixTimeSeconds(),
                Limit = 100
            });

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Result.Should().HaveCount(1); // Only the valid one
    }

    [Fact]
    public async Task GetClaimsCursor_NoWallet_NotFound()
    {
        var subject = _fixture.Create<string>();
        var controller = new ClaimsController(_claimMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var result = await controller.GetClaimsCursor(
            _unitOfWork,
            new GetClaimsQueryParametersCursor());

        result.Result.Should().BeOfType<NotFoundObjectResult>();
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
    public async Task AggregateClaims_OnlyReturnsClaimsWithinOneHour()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new ClaimsController(_claimMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var wallet = await _dbFixture.CreateWallet(subject);
        var endpoint = await _dbFixture.CreateWalletEndpoint(wallet);

        var baseTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

        // Claim 1: Production and consumption start at the same time (within 0 minutes)
        {
            var prodCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Production, baseTime, baseTime.AddHours(1));
            var prodSlice = await _dbFixture.CreateSlice(endpoint, prodCert, new PedersenCommitment.SecretCommitmentInfo(100));
            var consCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Consumption, baseTime, baseTime.AddHours(1));
            var consSlice = await _dbFixture.CreateSlice(endpoint, consCert, new PedersenCommitment.SecretCommitmentInfo(100));
            await _dbFixture.CreateClaim(prodSlice, consSlice, Models.ClaimState.Claimed);
        }

        // Claim 2: Consumption starts 59 minutes after production (within 60 minutes)
        {
            var consStart = baseTime.AddMinutes(59);
            var prodCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Production, baseTime, baseTime.AddHours(1));
            var prodSlice = await _dbFixture.CreateSlice(endpoint, prodCert, new PedersenCommitment.SecretCommitmentInfo(100));
            var consCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Consumption, consStart, consStart.AddHours(1));
            var consSlice = await _dbFixture.CreateSlice(endpoint, consCert, new PedersenCommitment.SecretCommitmentInfo(100));
            await _dbFixture.CreateClaim(prodSlice, consSlice, Models.ClaimState.Claimed);
        }

        // Claim 2: Consumption starts 60 minutes after production (should be excluded)
        {
            var consStart = baseTime.AddMinutes(60);
            var prodCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Production, baseTime, baseTime.AddHours(1));
            var prodSlice = await _dbFixture.CreateSlice(endpoint, prodCert, new PedersenCommitment.SecretCommitmentInfo(100));
            var consCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Consumption, consStart, consStart.AddHours(1));
            var consSlice = await _dbFixture.CreateSlice(endpoint, consCert, new PedersenCommitment.SecretCommitmentInfo(100));
            await _dbFixture.CreateClaim(prodSlice, consSlice, Models.ClaimState.Claimed);
        }

        // Claim 3: Consumption starts 61 minutes after production (should be excluded)
        {
            var consStart = baseTime.AddMinutes(61);
            var prodCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Production, baseTime, baseTime.AddHours(1));
            var prodSlice = await _dbFixture.CreateSlice(endpoint, prodCert, new PedersenCommitment.SecretCommitmentInfo(100));
            var consCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _fixture.Create<string>(), Models.GranularCertificateType.Consumption, consStart, consStart.AddHours(1));
            var consSlice = await _dbFixture.CreateSlice(endpoint, consCert, new PedersenCommitment.SecretCommitmentInfo(100));
            await _dbFixture.CreateClaim(prodSlice, consSlice, Models.ClaimState.Claimed);
        }

        // Act
        var result = await controller.AggregateClaims(
            _unitOfWork,
            new AggregateClaimsQueryParameters
            {
                TimeAggregate = TimeAggregate.Actual,
                TimeZone = "UTC",
                Start = baseTime.AddHours(-1).ToUnixTimeSeconds(),
                End = baseTime.AddHours(3).ToUnixTimeSeconds()
            });

        // Assert
        result.Value.Should().NotBeNull();
        var resultList = result.Value!.Result;

        // Returns the 2 claim results as an aggregate with 1 entry, but combined claim amount 100 + 100
        resultList.Should().HaveCount(1).And.Subject.Sum(x => x.Quantity).Should().Be(200);
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
    public async Task AggregateClaims_NoWallet_NotFound()
    {
        var subject = _fixture.Create<string>();
        var controller = new ClaimsController(_claimMetrics)
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var result = await controller.AggregateClaims(
            _unitOfWork,
            new AggregateClaimsQueryParameters
            {
                TimeAggregate = TimeAggregate.Day,
                TimeZone = "Europe/Copenhagen"
            });


        result.Result.Should().BeOfType<NotFoundObjectResult>();
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
