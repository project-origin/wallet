using System.Security.Claims;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.IntegrationTests.TestExtensions;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Services.REST.v1;
using Renci.SshNet;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class WalletControllerTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly Fixture _fixture;
    private readonly Secp256k1Algorithm _hdAlgorithm;
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly IUnitOfWork _unitOfWork;

    public WalletControllerTests(PostgresDatabaseFixture postgresDatabaseFixture)
    {
        _fixture = new Fixture();
        _hdAlgorithm = new Secp256k1Algorithm();
        _dbFixture = postgresDatabaseFixture;
        _unitOfWork = _dbFixture.CreateUnitOfWork();
    }

    [Fact]
    public async Task Verify_Unauthorized()
    {
        // Arrange
        var controller = new WalletController();

        // Act
        var result = await controller.CreateWallet(
            _unitOfWork,
            _hdAlgorithm,
            new CreateWalletRequest());

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Verify_InvalidKey()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.CreateWallet(
            _unitOfWork,
            _hdAlgorithm,
            new CreateWalletRequest()
            {
                PrivateKey = _fixture.Create<byte[]>()
            });

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be("Invalid private key.");
    }

    [Fact]
    public async Task Verify_Valid()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.CreateWallet(
            _unitOfWork,
            _hdAlgorithm,
            new CreateWalletRequest());

        // Assert
        result
            .Result.Should().BeOfType<CreatedResult>()
            .Which.Value.Should().BeOfType<CreateWalletResponse>()
            .Which.WalletId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Verify_OnlySingleAllowedPerSubject()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var result1 = await controller.CreateWallet(
            _unitOfWork,
            _hdAlgorithm,
            new CreateWalletRequest());

        result1.Result.Should().BeOfType<CreatedResult>();

        // Act
        var result2 = await controller.CreateWallet(
            _unitOfWork,
            _hdAlgorithm,
            new CreateWalletRequest());

        // Assert
        result2.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be("Wallet already exists.");
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
