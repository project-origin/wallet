using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MsOptions = Microsoft.Extensions.Options;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using ProjectOrigin.Vault.Tests.TestExtensions;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Options;
using ProjectOrigin.Vault.Services.REST.v1;
using Xunit;

namespace ProjectOrigin.Vault.Tests;

public class WalletControllerTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly Fixture _fixture;
    private readonly Secp256k1Algorithm _hdAlgorithm;
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MsOptions.IOptions<ServiceOptions> _options;

    public WalletControllerTests(PostgresDatabaseFixture postgresDatabaseFixture)
    {
        _fixture = new Fixture();
        _hdAlgorithm = new Secp256k1Algorithm();
        _dbFixture = postgresDatabaseFixture;
        _unitOfWork = _dbFixture.CreateUnitOfWork();

        _options = MsOptions.Options.Create(new ServiceOptions
        {
            EndpointAddress = new Uri("https://example.com"),
            PathBase = new PathString("/foo-bar-baz"),
        });
    }

    [Fact]
    public async Task DisableWallet_Verify_Unauthorized()
    {
        // Arrange
        var controller = new WalletController();

        // Act
        var result = await controller.DisableWallet(_unitOfWork, Guid.NewGuid());

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DisableWallet_WhenNoWallet_NotFound()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.DisableWallet(_unitOfWork, Guid.NewGuid());

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DisableWallet_WhenWalletIsDisabled_BadRequest()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var wallet = await _dbFixture.CreateWallet(subject);
        await _dbFixture.DisableWallet(wallet.Id);

        // Act
        var result = await controller.DisableWallet(_unitOfWork, wallet.Id);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DisableWallet()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var wallet = await _dbFixture.CreateWallet(subject);

        // Act
        var result = await controller.DisableWallet(_unitOfWork, wallet.Id);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var walletDb = await _unitOfWork.WalletRepository.GetWallet(wallet.Id);
        Assert.NotNull(walletDb);
        Assert.NotNull(walletDb.Disabled);
    }

    // Tests for EnableWallet endpoint
    [Fact]
    public async Task EnableWallet_Verify_Unauthorized()
    {
        // Arrange
        var controller = new WalletController();

        // Act
        var result = await controller.EnableWallet(_unitOfWork, Guid.NewGuid());

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task EnableWallet_WhenNoWallet_NotFound()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.EnableWallet(_unitOfWork, Guid.NewGuid());

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task EnableWallet_WhenWalletIsAlreadyEnabled()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var wallet = await _dbFixture.CreateWallet(subject);

        // Act
        var result = await controller.EnableWallet(_unitOfWork, wallet.Id);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var walletDb = await _unitOfWork.WalletRepository.GetWallet(wallet.Id);
        Assert.NotNull(walletDb);
        Assert.Null(walletDb.Disabled);
    }

    [Fact]
    public async Task EnableWallet()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var wallet = await _dbFixture.CreateWallet(subject);
        await _dbFixture.DisableWallet(wallet.Id);

        // Act
        var result = await controller.EnableWallet(_unitOfWork, wallet.Id);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var walletDb = await _unitOfWork.WalletRepository.GetWallet(wallet.Id);
        Assert.NotNull(walletDb);
        Assert.Null(walletDb.Disabled);
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
            _options,
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
            _options,
            new CreateWalletRequest()
            {
                PrivateKey = _fixture.Create<byte[]>()
            });

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be("Invalid private key.");
    }

    [Fact]
    public async Task Verify_ValidWithValidKey()
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
            _options,
            new CreateWalletRequest()
            {
                PrivateKey = _hdAlgorithm.GenerateNewPrivateKey().Export().ToArray()
            });

        // Assert
        result
            .Result.Should().BeOfType<CreatedResult>()
            .Which.Value.Should().BeOfType<CreateWalletResponse>()
            .Which.WalletId.Should().NotBeEmpty();

        var wallet = await _unitOfWork.WalletRepository.GetWallet(subject);
        wallet.Should().NotBeNull();
        wallet!.PrivateKey.Should().BeEquivalentTo(_hdAlgorithm.GenerateNewPrivateKey());
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
            _options,
            new CreateWalletRequest());

        // Assert
        result
            .Result.Should().BeOfType<CreatedResult>()
            .Which.Value.Should().BeOfType<CreateWalletResponse>()
            .Which.WalletId.Should().NotBeEmpty();

        var wallet = await _unitOfWork.WalletRepository.GetWallet(subject);
        wallet.Should().NotBeNull();
        wallet!.PrivateKey.Should().NotBeNull();
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

        var firstCreateResult = await controller.CreateWallet(
            _unitOfWork,
            _hdAlgorithm,
            _options,
            new CreateWalletRequest());

        firstCreateResult.Result.Should().BeOfType<CreatedResult>();

        // Act
        var secondCreateResult = await controller.CreateWallet(
            _unitOfWork,
            _hdAlgorithm,
            _options,
            new CreateWalletRequest());

        // Assert
        secondCreateResult.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be("Wallet already exists.");
    }

    [Fact]
    public async Task Verify_GetWallets()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var createResult = await controller.CreateWallet(
            _unitOfWork,
            _hdAlgorithm,
            _options,
            new CreateWalletRequest());

        var response = createResult.Result.Should().BeOfType<CreatedResult>()
            .Which.Value.Should().BeOfType<CreateWalletResponse>().Which;

        // Act
        var getResult = await controller.GetWallets(_unitOfWork);

        // Assert
        getResult.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<ResultList<WalletRecord, PageInfo>>()
            .Which.Result.Should().ContainSingle()
            .Which.Id.Should().Be(response!.WalletId);
    }

    [Fact]
    public async Task Verify_GetWallets_NotFound()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var getResult = await controller.GetWallets(_unitOfWork);

        // Assert
        getResult.Result.Should().BeOfType<OkObjectResult>()
          .Which.Value.Should().BeOfType<ResultList<WalletRecord, PageInfo>>()
          .Which.Result.Should().BeEmpty();
    }

    [Fact]
    public async Task Verify_GetWallets_Unauthorized()
    {
        // Arrange
        var controller = new WalletController();

        // Act
        var getResult = await controller.GetWallets(_unitOfWork);

        // Assert
        getResult.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Verify_GetWallet_WithWalletId()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var createResult = await controller.CreateWallet(
            _unitOfWork,
            _hdAlgorithm,
            _options,
            new CreateWalletRequest());

        var createdResponse = createResult.Result.Should().BeOfType<CreatedResult>()
            .Which.Value.Should().BeOfType<CreateWalletResponse>().Which;

        // Act
        var getResult = await controller.GetWallet(_unitOfWork, createdResponse.WalletId);

        // Assert
        getResult.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<WalletRecord>()
            .Which.Id.Should().Be(createdResponse.WalletId);
    }

    [Fact]
    public async Task Verify_GetWalletWithId_Unauthorized()
    {
        // Arrange
        var controller = new WalletController();

        // Act
        var getResult = await controller.GetWallet(_unitOfWork, Guid.NewGuid());

        // Assert
        getResult.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Verify_GetWalletWithId_NotFound()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var getResult = await controller.GetWallet(_unitOfWork, Guid.NewGuid());

        // Assert
        getResult.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Verify_GetWalletWithId_ForDifferentUser_NotFound()
    {
        // Arrange
        var subject1 = _fixture.Create<string>();
        var controller1 = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject1)
        };


        var subject2 = _fixture.Create<string>();
        var controller2 = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject2)
        };

        var createResult = await controller1.CreateWallet(
            _unitOfWork,
            _hdAlgorithm,
            _options,
            new CreateWalletRequest());

        var createdResponse = createResult.Result.Should().BeOfType<CreatedResult>()
            .Which.Value.Should().BeOfType<CreateWalletResponse>().Which;

        // Act
        var getResult = await controller1.GetWallet(_unitOfWork, createdResponse.WalletId);

        // Assert
        getResult.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<WalletRecord>()
            .Which.Id.Should().Be(createdResponse.WalletId);

        // Act
        var getResultForDifferentUser = await controller2.GetWallet(_unitOfWork, createdResponse.WalletId);

        // Assert
        getResultForDifferentUser.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Verify_CreateWalletEndpoint_Unauthorized()
    {
        // Arrange
        var controller = new WalletController();

        // Act
        var result = await controller.CreateWalletEndpoint(
            _unitOfWork,
            _options,
            Guid.NewGuid());

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Verify_CreateWalletEndpoint_WalletNotFound()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.CreateWalletEndpoint(
            _unitOfWork,
            _options,
            Guid.NewGuid());

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateWalletEndpoint_WhenWalletIsDisabled_BadRequest()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var wallet = await _dbFixture.CreateWallet(subject);
        await _dbFixture.DisableWallet(wallet.Id);

        // Act
        var result = await controller.CreateWalletEndpoint(
            _unitOfWork,
            _options,
            wallet.Id);


        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Verify_CreateWalletEndpoint_Success()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var createResult = await controller.CreateWallet(
            _unitOfWork,
            _hdAlgorithm,
            _options,
            new CreateWalletRequest());

        var createdResponse = createResult.Result.Should().BeOfType<CreatedResult>()
            .Which.Value.Should().BeOfType<CreateWalletResponse>().Which;

        // Act
        var result = await controller.CreateWalletEndpoint(
            _unitOfWork,
            _options,
            createdResponse.WalletId
            );

        // Assert
        var response = result.Result.Should().BeOfType<CreatedResult>()
            .Which.Value.Should().BeOfType<CreateWalletEndpointResponse>().Which;

        response.WalletReference.Version.Should().Be(1);
        response.WalletReference.Endpoint.Should().BeEquivalentTo(new Uri(_options.Value.EndpointAddress, Path.Combine(_options.Value.PathBase, "v1/slices")));
        response.WalletReference.PublicKey.Should().NotBeNull();

        var endpointsFound = await _unitOfWork.WalletRepository.GetWalletEndpoint(response.WalletReference.PublicKey);
        endpointsFound.Should().NotBeNull();
    }

    [Fact]
    public async Task Verify_CreateExternalEndpoint_Unauthorized()
    {
        // Arrange
        var controller = new WalletController();

        // Act
        var result = await controller.CreateExternalEndpoint(
            _unitOfWork,
            new CreateExternalEndpointRequest()
            {
                TextReference = _fixture.Create<string>(),
                WalletReference = new WalletEndpointReference()
                {
                    PublicKey = _hdAlgorithm.GenerateNewPrivateKey().Neuter(),
                    Version = 1,
                    Endpoint = new Uri("https://example.com")
                }
            });

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Verify_CreateExternalEndpoint_Success()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        // Act
        var result = await controller.CreateExternalEndpoint(
            _unitOfWork,
            new CreateExternalEndpointRequest()
            {
                TextReference = _fixture.Create<string>(),
                WalletReference = new WalletEndpointReference()
                {
                    PublicKey = _hdAlgorithm.GenerateNewPrivateKey().Neuter(),
                    Version = 1,
                    Endpoint = new Uri("https://example.com")
                }
            });

        // Assert
        var response = result.Result.Should().BeOfType<CreatedResult>()
            .Which.Value.Should().BeOfType<CreateExternalEndpointResponse>().Which;

        response.ReceiverId.Should().NotBeEmpty();

        var endpointFound = await _unitOfWork.WalletRepository.GetExternalEndpoint(response.ReceiverId);
        endpointFound.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateExternalEndpoint_WhenKnownWalletAndItIsDisabled_BadRequest()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var wallet = await _dbFixture.CreateWallet(subject);
        var createEndpointResponse = await controller.CreateWalletEndpoint(
            _unitOfWork,
            _options,
            wallet.Id
        );

        var endpointCreatedResult = createEndpointResponse.Result.Should().BeOfType<CreatedResult>()
            .Which.Value.Should().BeOfType<CreateWalletEndpointResponse>().Which;

        await _dbFixture.DisableWallet(wallet.Id);

        // Act
        var result = await controller.CreateExternalEndpoint(
            _unitOfWork,
            new CreateExternalEndpointRequest()
            {
                TextReference = _fixture.Create<string>(),
                WalletReference = endpointCreatedResult.WalletReference
            });

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateExternalEndpoint_SelfReference_Invalid()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new WalletController()
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var createWalletResult = await controller.CreateWallet(
            _unitOfWork,
            _hdAlgorithm,
            _options,
            new CreateWalletRequest());

        var walletCreationResult = createWalletResult.Result.Should().BeOfType<CreatedResult>()
            .Which.Value.Should().BeOfType<CreateWalletResponse>().Which;

        var createEndpointResponse = await controller.CreateWalletEndpoint(
            _unitOfWork,
            _options,
            walletCreationResult.WalletId
            );

        var endpointCreatedResult = createEndpointResponse.Result.Should().BeOfType<CreatedResult>()
            .Which.Value.Should().BeOfType<CreateWalletEndpointResponse>().Which;


        // Act
        var result = await controller.CreateExternalEndpoint(
            _unitOfWork,
            new CreateExternalEndpointRequest()
            {
                TextReference = _fixture.Create<string>(),
                WalletReference = endpointCreatedResult.WalletReference
            });

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be("Cannot create receiver deposit endpoint to self.");
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
