using AutoFixture;
using FluentAssertions;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.WalletSystem.Server.Activities.Exceptions;
using ProjectOrigin.WalletSystem.Server.CommandHandlers;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Projections;
using ProjectOrigin.WalletSystem.Server.Repositories;
using ProjectOrigin.WalletSystem.Server.Services;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.CommandHandlers;

public class VerifySliceCommandHandlerTests : IAsyncLifetime
{
    const string RegistryName = "MyRegistry";
    const string Area = "Narnia";

    private Fixture _fixture;
    private ICertificateRepository _certificateRepository;
    private IWalletRepository _walletRepository;
    private ILogger<VerifySliceCommandHandler> _logger;
    private IRegistryService _registryService;
    private ServiceProvider _provider;

    public VerifySliceCommandHandlerTests()
    {
        _fixture = new Fixture();

        _certificateRepository = Substitute.For<ICertificateRepository>();
        _walletRepository = Substitute.For<IWalletRepository>();
        _logger = Substitute.For<ILogger<VerifySliceCommandHandler>>();

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.WalletRepository.Returns(_walletRepository);
        unitOfWork.CertificateRepository.Returns(_certificateRepository);

        _registryService = Substitute.For<IRegistryService>();
        _provider = new ServiceCollection()
            .AddSingleton(unitOfWork)
            .AddSingleton(_registryService)
            .AddSingleton(_logger)
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<VerifySliceCommandHandler>();
            })
            .BuildServiceProvider(true);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    [Fact]
    public async void WhenReceivedSliceIsValid_ExpectIsConvertedToSliceAndCertificateIsCreated()
    {
        // Arrange
        var certId = Guid.NewGuid();
        IHDPrivateKey privateKey = new Secp256k1Algorithm().GenerateNewPrivateKey();
        var walletPosition = 1;
        var depositPosition = 1;
        var commitment = new SecretCommitmentInfo(150);

        var depositEndpoint = new DepositEndpoint
        {
            Id = Guid.NewGuid(),
            WalletId = Guid.NewGuid(),
            WalletPosition = walletPosition,
            PublicKey = privateKey.Derive(walletPosition).Neuter(),
            Owner = string.Empty,
            ReferenceText = string.Empty,
            Endpoint = string.Empty
        };

        _walletRepository.GetDepositEndpoint(depositEndpoint.Id).Returns(depositEndpoint);

        var issuedEvent = CreateIssuedEvent(certId, commitment, depositEndpoint.PublicKey.Derive(depositPosition).GetPublicKey());
        var certificate = new GranularCertificate(issuedEvent);

        _registryService.GetGranularCertificate(RegistryName, certId).Returns(new GetCertificateResult.Success(certificate));

        var harness = _provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var command = new VerifySliceCommand
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = depositEndpoint.Id,
            DepositEndpointPosition = depositPosition,
            Registry = RegistryName,
            CertificateId = certId,
            Quantity = commitment.Message,
            RandomR = commitment.BlindingValue.ToArray()
        };

        await harness.Bus.Publish(command);

        // Act
        var message = await harness.Consumed.SelectAsync<VerifySliceCommand>().First();

        // Assert
        message.Exception.Should().BeNull();

        await _certificateRepository.Received(1).InsertCertificate(Arg.Is<Certificate>(x => x.Id == certId
                                                                                            && x.StartDate == issuedEvent.Period.Start.ToDateTimeOffset()
                                                                                            && x.EndDate == issuedEvent.Period.End.ToDateTimeOffset()));
        var blindingValueArray = commitment.BlindingValue.ToArray();
        await _certificateRepository.Received(1).InsertSlice(Arg.Is<Slice>(x => x.CertificateId == certId
                                                                                && x.Quantity == commitment.Message
                                                                                && x.RandomR.SequenceEqual(blindingValueArray)
                                                                                && x.SliceState == SliceState.Available));
    }

    [Fact]
    public async void WhenCertificateNotFound_CompleteAndWriteWarning()
    {
        // Arrange
        var certId = Guid.NewGuid();
        IHDPrivateKey privateKey = new Secp256k1Algorithm().GenerateNewPrivateKey();
        var walletPosition = 1;
        var depositPosition = 1;
        var commitment = new SecretCommitmentInfo(150);

        var depositEndpoint = new DepositEndpoint
        {
            Id = Guid.NewGuid(),
            WalletId = Guid.NewGuid(),
            WalletPosition = walletPosition,
            PublicKey = privateKey.Derive(walletPosition).Neuter(),
            Owner = string.Empty,
            ReferenceText = string.Empty,
            Endpoint = string.Empty
        };
        _walletRepository.GetDepositEndpoint(depositEndpoint.Id).Returns(depositEndpoint);

        _registryService.GetGranularCertificate(RegistryName, certId).Returns(new GetCertificateResult.NotFound());  // <-- failure

        var harness = _provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var command = new VerifySliceCommand
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = depositEndpoint.Id,
            DepositEndpointPosition = depositPosition,
            Registry = RegistryName,
            CertificateId = certId,
            Quantity = commitment.Message,
            RandomR = commitment.BlindingValue.ToArray()
        };

        await harness.Bus.Publish(command);

        // Act
        var message = await harness.Consumed.SelectAsync<VerifySliceCommand>().First();

        // Assert
        message.Exception.Should().BeNull();

        _logger.Received(1).LogWarning($"GranularCertificate with id {certId} not found in registry {RegistryName}");

        await _certificateRepository.DidNotReceiveWithAnyArgs().InsertCertificate(default!);
        await _certificateRepository.DidNotReceiveWithAnyArgs().InsertSlice(default!);
    }

    [Fact]
    public async void WhenTransientException_FaultedWithTransient()
    {
        var innerException = new Exception("Could not connect to registry");

        // Arrange
        var certId = Guid.NewGuid();
        IHDPrivateKey privateKey = new Secp256k1Algorithm().GenerateNewPrivateKey();
        var walletPosition = 1;
        var depositPosition = 1;
        var commitment = new SecretCommitmentInfo(150);

        var depositEndpoint = new DepositEndpoint
        {
            Id = Guid.NewGuid(),
            WalletId = Guid.NewGuid(),
            WalletPosition = walletPosition,
            PublicKey = privateKey.Derive(walletPosition).Neuter(),
            Owner = string.Empty,
            ReferenceText = string.Empty,
            Endpoint = string.Empty
        };
        _walletRepository.GetDepositEndpoint(depositEndpoint.Id).Returns(depositEndpoint);

        _registryService.GetGranularCertificate(RegistryName, certId).Returns(new GetCertificateResult.TransientFailure(innerException));  // <-- failure

        var harness = _provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var command = new VerifySliceCommand
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = depositEndpoint.Id,
            DepositEndpointPosition = depositPosition,
            Registry = RegistryName,
            CertificateId = certId,
            Quantity = commitment.Message,
            RandomR = commitment.BlindingValue.ToArray()
        };

        await harness.Bus.Publish(command);

        // Act
        var message = await harness.Consumed.SelectAsync<VerifySliceCommand>().First();

        // Assert
        message.Exception.Should().NotBeNull();
        message.Exception.Should().BeOfType<TransientException>().Which.InnerException.Should().Be(innerException);

        _logger.Received(1).LogWarning(innerException, $"Transient failed to get GranularCertificate with id {certId} on registry {RegistryName}");

        await _certificateRepository.DidNotReceiveWithAnyArgs().InsertCertificate(default!);
        await _certificateRepository.DidNotReceiveWithAnyArgs().InsertSlice(default!);
    }

    [Fact]
    public async void WhenFailure_FaultedWithTransient()
    {
        var innerException = new Exception("Could not connect to registry");

        // Arrange
        var certId = Guid.NewGuid();
        IHDPrivateKey privateKey = new Secp256k1Algorithm().GenerateNewPrivateKey();
        var walletPosition = 1;
        var depositPosition = 1;
        var commitment = new SecretCommitmentInfo(150);

        var depositEndpoint = new DepositEndpoint
        {
            Id = Guid.NewGuid(),
            WalletId = Guid.NewGuid(),
            WalletPosition = walletPosition,
            PublicKey = privateKey.Derive(walletPosition).Neuter(),
            Owner = string.Empty,
            ReferenceText = string.Empty,
            Endpoint = string.Empty
        };
        _walletRepository.GetDepositEndpoint(depositEndpoint.Id).Returns(depositEndpoint);

        _registryService.GetGranularCertificate(RegistryName, certId).Returns(new GetCertificateResult.Failure(innerException)); // <-- failure

        var harness = _provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var command = new VerifySliceCommand
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = depositEndpoint.Id,
            DepositEndpointPosition = depositPosition,
            Registry = RegistryName,
            CertificateId = certId,
            Quantity = commitment.Message,
            RandomR = commitment.BlindingValue.ToArray()
        };

        await harness.Bus.Publish(command);

        // Act
        var message = await harness.Consumed.SelectAsync<VerifySliceCommand>().First();

        // Assert
        message.Exception.Should().NotBeNull();
        message.Exception.Should().BeOfType<Exception>().Which.InnerException.Should().Be(innerException);

        _logger.Received(1).LogError(innerException, $"Failed to get certificate with {certId}");

        await _certificateRepository.DidNotReceiveWithAnyArgs().InsertCertificate(default!);
        await _certificateRepository.DidNotReceiveWithAnyArgs().InsertSlice(default!);
    }

    [Fact]
    public async void WhenSliceNotFound()
    {
        // Arrange
        var certId = Guid.NewGuid();
        IHDPrivateKey privateKey = new Secp256k1Algorithm().GenerateNewPrivateKey();
        var walletPosition = 1;
        var depositPosition = 1;
        var commitmentIssued = new SecretCommitmentInfo(150);
        var commitmentSent = new SecretCommitmentInfo(150);

        var depositEndpoint = new DepositEndpoint
        {
            Id = Guid.NewGuid(),
            WalletId = Guid.NewGuid(),
            WalletPosition = walletPosition,
            PublicKey = privateKey.Derive(walletPosition).Neuter(),
            Owner = string.Empty,
            ReferenceText = string.Empty,
            Endpoint = string.Empty
        };
        _walletRepository.GetDepositEndpoint(depositEndpoint.Id).Returns(depositEndpoint);

        var issuedEvent = CreateIssuedEvent(certId, commitmentIssued, depositEndpoint.PublicKey.Derive(depositPosition).GetPublicKey());
        var certificate = new GranularCertificate(issuedEvent);

        _registryService.GetGranularCertificate(RegistryName, certId).Returns(new GetCertificateResult.Success(certificate));

        var harness = _provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var command = new VerifySliceCommand
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = depositEndpoint.Id,
            DepositEndpointPosition = depositPosition,
            Registry = RegistryName,
            CertificateId = certId,
            Quantity = commitmentSent.Message, // <-- Wrong commitment
            RandomR = commitmentSent.BlindingValue.ToArray()
        };

        await harness.Bus.Publish(command);

        // Act
        var message = await harness.Consumed.SelectAsync<VerifySliceCommand>().First();

        // Assert
        message.Exception.Should().BeNull();

        var sliceId = ByteString.CopyFrom(SHA256.HashData(commitmentSent.Commitment.C));
        _logger.Received(1).LogWarning($"Slice with id {Convert.ToBase64String(sliceId.Span)} not found in certificate {certId}");

        await _certificateRepository.DidNotReceiveWithAnyArgs().InsertCertificate(default!);
        await _certificateRepository.DidNotReceiveWithAnyArgs().InsertSlice(default!);
    }

    [Fact]
    public async void WhenInvalidKey()
    {
        // Arrange
        var certId = Guid.NewGuid();
        IHDPrivateKey privateKey = new Secp256k1Algorithm().GenerateNewPrivateKey();
        var walletPosition = 1;
        var depositPosition = 1;
        var commitment = new SecretCommitmentInfo(150);

        var depositEndpoint = new DepositEndpoint
        {
            Id = Guid.NewGuid(),
            WalletId = Guid.NewGuid(),
            WalletPosition = walletPosition,
            PublicKey = privateKey.Derive(walletPosition).Neuter(),
            Owner = string.Empty,
            ReferenceText = string.Empty,
            Endpoint = string.Empty
        };
        _walletRepository.GetDepositEndpoint(depositEndpoint.Id).Returns(depositEndpoint);

        var issuedEvent = CreateIssuedEvent(certId, commitment, depositEndpoint.PublicKey.Derive(depositPosition).GetPublicKey());
        var certificate = new GranularCertificate(issuedEvent);

        _registryService.GetGranularCertificate(RegistryName, certId).Returns(new GetCertificateResult.Success(certificate));

        var harness = _provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var command = new VerifySliceCommand
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = depositEndpoint.Id,
            DepositEndpointPosition = depositPosition + 1, // <-- Wrong position thereby key
            Registry = RegistryName,
            CertificateId = certId,
            Quantity = commitment.Message,
            RandomR = commitment.BlindingValue.ToArray()
        };

        await harness.Bus.Publish(command);

        // Act
        var message = await harness.Consumed.SelectAsync<VerifySliceCommand>().First();

        // Assert
        message.Exception.Should().BeNull();

        var sliceId = ByteString.CopyFrom(SHA256.HashData(commitment.Commitment.C));
        _logger.Received(1).LogWarning($"Not correct publicKey on {certId}");

        await _certificateRepository.DidNotReceiveWithAnyArgs().InsertCertificate(default!);
        await _certificateRepository.DidNotReceiveWithAnyArgs().InsertSlice(default!);
    }

    private static IssuedEvent CreateIssuedEvent(Guid certId, SecretCommitmentInfo commitment, IPublicKey publicKey) => new IssuedEvent
    {
        CertificateId = new Common.V1.FederatedStreamId
        {
            Registry = RegistryName,
            StreamId = new Common.V1.Uuid { Value = certId.ToString() }
        },
        Type = Electricity.V1.GranularCertificateType.Production,
        Period = new DateInterval { Start = Timestamp.FromDateTimeOffset(DateTimeOffset.Now), End = Timestamp.FromDateTimeOffset(DateTimeOffset.Now.AddHours(1)) },
        GridArea = Area,
        QuantityCommitment = new Electricity.V1.Commitment
        {
            Content = ByteString.CopyFrom(commitment.Commitment.C),
            RangeProof = ByteString.CopyFrom(commitment.CreateRangeProof(certId.ToString()))
        },
        OwnerPublicKey = new PublicKey
        {
            Content = ByteString.CopyFrom(publicKey.Export()),
            Type = KeyType.Secp256K1
        }
    };

}
