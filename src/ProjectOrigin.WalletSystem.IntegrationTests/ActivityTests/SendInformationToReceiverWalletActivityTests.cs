using System;
using System.Text.Json;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.WalletSystem.Server.Activities;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Options;
using ProjectOrigin.WalletSystem.Server.Repositories;
using ProjectOrigin.WalletSystem.Server.Serialization;
using ProjectOrigin.WalletSystem.Server.Services.REST.v1;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.ActivityTests;

public class SendInformationToReceiverWalletActivityTests
{
    private readonly SendInformationToReceiverWalletActivity _activity;
    private readonly MassTransit.ExecuteContext<SendInformationToReceiverWalletArgument> _context;
    private readonly Fixture _fixture;
    private readonly ITransferRepository _transferRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly ICertificateRepository _certificateRepository;
    private readonly string _endpoint = "http://test.com";

    public SendInformationToReceiverWalletActivityTests()
    {
        _fixture = new Fixture();
        _transferRepository = Substitute.For<ITransferRepository>();
        _walletRepository = Substitute.For<IWalletRepository>();
        _certificateRepository = Substitute.For<ICertificateRepository>();

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.TransferRepository.Returns(_transferRepository);
        unitOfWork.WalletRepository.Returns(_walletRepository);
        unitOfWork.CertificateRepository.Returns(_certificateRepository);

        _activity = new SendInformationToReceiverWalletActivity(
            unitOfWork,
            Options.Create(new ServiceOptions
            {
                EndpointAddress = new Uri(_endpoint)
            }),
            Substitute.For<ILogger<SendInformationToReceiverWalletActivity>>()
        );
        _context = Substitute.For<MassTransit.ExecuteContext<SendInformationToReceiverWalletArgument>>();
    }

    [Fact]
    public async Task SendOverRestToExternalWallet_Valid()
    {
        // Arrange
        var wireMockServer = WireMockServer.Start();
        var hdAlgorithm = new Secp256k1Algorithm();

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new IHDPublicKeyConverter(hdAlgorithm));

        var owner = _fixture.Create<string>();
        var endpoint = new ExternalEndpoint
        {
            Id = Guid.NewGuid(),
            Endpoint = $"{wireMockServer.Urls[0]}/v1/slices",
            PublicKey = hdAlgorithm.GenerateNewPrivateKey().Neuter(),
            Owner = owner,
            ReferenceText = _fixture.Create<string>(),
        };
        _walletRepository.GetExternalEndpoint(Arg.Any<Guid>()).Returns(endpoint);

        wireMockServer.Given(Request.Create().WithPath("/v1/slices").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(201));

        var transferredSlice = new TransferredSlice
        {
            Id = Guid.NewGuid(),
            CertificateId = Guid.NewGuid(),
            Quantity = _fixture.Create<int>(),
            ExternalEndpointId = endpoint.Id,
            ExternalEndpointPosition = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            RegistryName = _fixture.Create<string>(),
            State = TransferredSliceState.Registering
        };
        _transferRepository.GetTransferredSlice(Arg.Any<Guid>()).Returns(transferredSlice);

        var attribute1 = new WalletAttribute
        {
            CertificateId = transferredSlice.CertificateId,
            RegistryName = transferredSlice.RegistryName,
            Key = _fixture.Create<string>(),
            Value = _fixture.Create<string>(),
            Salt = _fixture.Create<byte[]>()
        };
        var attribute2 = new WalletAttribute
        {
            CertificateId = transferredSlice.CertificateId,
            RegistryName = transferredSlice.RegistryName,
            Key = _fixture.Create<string>(),
            Value = _fixture.Create<string>(),
            Salt = _fixture.Create<byte[]>()
        };
        _context.Arguments.Returns(new SendInformationToReceiverWalletArgument()
        {
            ExternalEndpointId = endpoint.Id,
            SliceId = transferredSlice.Id,
            WalletAttributes = [
                attribute1,
                attribute2
            ],
            RequestId = Guid.NewGuid(),
            Owner = owner
        });

        // Act
        await _activity.Execute(_context);

        // Assert
        await _transferRepository.Received(1).SetTransferredSliceState(Arg.Any<Guid>(), TransferredSliceState.Transferred);

        var jsonBody = JsonSerializer.Serialize(new ReceiveRequest
        {
            PublicKey = endpoint.PublicKey.Export().ToArray(),
            Position = (uint)transferredSlice.ExternalEndpointPosition,
            CertificateId = new FederatedStreamId
            {
                Registry = transferredSlice.RegistryName,
                StreamId = transferredSlice.CertificateId
            },
            Quantity = (uint)transferredSlice.Quantity,
            RandomR = transferredSlice.RandomR,
            HashedAttributes = new[]
            {
                new HashedAttribute
                {
                    Key = attribute1.Key,
                    Value = attribute1.Value,
                    Salt = attribute1.Salt
                },
                new HashedAttribute
                {
                    Key = attribute2.Key,
                    Value = attribute2.Value,
                    Salt = attribute2.Salt
                }
            }
        }, options);

        wireMockServer.FindLogEntries(Request.Create().WithPath("/v1/slices").WithBody(jsonBody).UsingPost()).Should().HaveCount(1);
    }


    [Fact]
    public async Task SendOverRestToExternalWallet_EndpointNotExisting_404()
    {
        // Arrange
        var nonExistingEndpoint = "http://example.com";
        var hdAlgorithm = new Secp256k1Algorithm();

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new IHDPublicKeyConverter(hdAlgorithm));

        var owner = _fixture.Create<string>();
        var endpoint = new ExternalEndpoint
        {
            Id = Guid.NewGuid(),
            Endpoint = $"{nonExistingEndpoint}/v1/slices",
            PublicKey = hdAlgorithm.GenerateNewPrivateKey().Neuter(),
            Owner = owner,
            ReferenceText = _fixture.Create<string>(),
        };
        _walletRepository.GetExternalEndpoint(Arg.Any<Guid>()).Returns(endpoint);

        var transferredSlice = new TransferredSlice
        {
            Id = Guid.NewGuid(),
            CertificateId = Guid.NewGuid(),
            Quantity = _fixture.Create<int>(),
            ExternalEndpointId = endpoint.Id,
            ExternalEndpointPosition = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            RegistryName = _fixture.Create<string>(),
            State = TransferredSliceState.Registering
        };
        _transferRepository.GetTransferredSlice(Arg.Any<Guid>()).Returns(transferredSlice);
        _context.Arguments.Returns(new SendInformationToReceiverWalletArgument()
        {
            ExternalEndpointId = endpoint.Id,
            SliceId = transferredSlice.Id,
            WalletAttributes = [],
            RequestId = Guid.NewGuid(),
            Owner = owner
        });

        // Act
        Func<Task> act = async () => await _activity.Execute(_context);

        // Assert
        await act.Should().ThrowAsync<System.Net.Http.HttpRequestException>()
            .WithMessage($"Response status code does not indicate success: 404 (Not Found).");

        await _transferRepository.Received(0).SetTransferredSliceState(Arg.Any<Guid>(), TransferredSliceState.Transferred);
    }

    [Fact]
    public async Task InsertIntoLocalWallet_Valid()
    {
        // Arrange
        var hdAlgorithm = new Secp256k1Algorithm();

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new IHDPublicKeyConverter(hdAlgorithm));

        var owner = _fixture.Create<string>();
        var externalEndpoint = new ExternalEndpoint
        {
            Id = Guid.NewGuid(),
            Endpoint = $"{_endpoint}/v1/slices",
            PublicKey = hdAlgorithm.GenerateNewPrivateKey().Neuter(),
            Owner = owner,
            ReferenceText = _fixture.Create<string>(),
        };
        _walletRepository.GetExternalEndpoint(externalEndpoint.Id).Returns(externalEndpoint);

        var walletEndpoint = new WalletEndpoint
        {
            Id = Guid.NewGuid(),
            PublicKey = externalEndpoint.PublicKey,
            IsRemainderEndpoint = false,
            WalletId = _fixture.Create<Guid>(),
            WalletPosition = _fixture.Create<int>(),
        };
        _walletRepository.GetWalletEndpoint(externalEndpoint.PublicKey).Returns(walletEndpoint);

        var transferredSlice = new TransferredSlice
        {
            Id = Guid.NewGuid(),
            CertificateId = Guid.NewGuid(),
            Quantity = _fixture.Create<int>(),
            ExternalEndpointId = externalEndpoint.Id,
            ExternalEndpointPosition = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            RegistryName = _fixture.Create<string>(),
            State = TransferredSliceState.Registering
        };
        _transferRepository.GetTransferredSlice(Arg.Any<Guid>()).Returns(transferredSlice);

        var attribute1 = new WalletAttribute
        {
            CertificateId = transferredSlice.CertificateId,
            RegistryName = transferredSlice.RegistryName,
            Key = _fixture.Create<string>(),
            Value = _fixture.Create<string>(),
            Salt = _fixture.Create<byte[]>()
        };
        var attribute2 = new WalletAttribute
        {
            CertificateId = transferredSlice.CertificateId,
            RegistryName = transferredSlice.RegistryName,
            Key = _fixture.Create<string>(),
            Value = _fixture.Create<string>(),
            Salt = _fixture.Create<byte[]>()
        };
        _context.Arguments.Returns(new SendInformationToReceiverWalletArgument()
        {
            ExternalEndpointId = externalEndpoint.Id,
            SliceId = transferredSlice.Id,
            WalletAttributes = [
                attribute1,
                attribute2
            ],
            RequestId = Guid.NewGuid(),
            Owner = owner
        });

        // Act
        await _activity.Execute(_context);

        // Assert
        await _transferRepository.Received(1).SetTransferredSliceState(Arg.Any<Guid>(), TransferredSliceState.Transferred);
        await _certificateRepository.Received(1).InsertWalletSlice(Arg.Is<WalletSlice>(slice =>
            slice.Id != Guid.Empty &&
            slice.RegistryName == transferredSlice.RegistryName &&
            slice.CertificateId == transferredSlice.CertificateId &&
            slice.Quantity == transferredSlice.Quantity &&
            slice.RandomR == transferredSlice.RandomR &&
            slice.WalletEndpointId == walletEndpoint.Id &&
            slice.WalletEndpointPosition == transferredSlice.ExternalEndpointPosition &&
            slice.State == WalletSliceState.Available));
        await _certificateRepository.Received(1).InsertWalletAttribute(walletEndpoint.WalletId, attribute1);
        await _certificateRepository.Received(1).InsertWalletAttribute(walletEndpoint.WalletId, attribute2);
    }

    [Fact]
    public async Task InsertIntoLocalWallet_MissingEndpoint()
    {
        // Arrange
        var hdAlgorithm = new Secp256k1Algorithm();

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new IHDPublicKeyConverter(hdAlgorithm));

        var owner = _fixture.Create<string>();
        var externalEndpoint = new ExternalEndpoint
        {
            Id = Guid.NewGuid(),
            Endpoint = $"{_endpoint}/v1/slices",
            PublicKey = hdAlgorithm.GenerateNewPrivateKey().Neuter(),
            Owner = owner,
            ReferenceText = _fixture.Create<string>(),
        };
        _walletRepository.GetExternalEndpoint(externalEndpoint.Id).Returns(externalEndpoint);
        WalletEndpoint? missingEndpoint = null;
        _walletRepository.GetWalletEndpoint(externalEndpoint.PublicKey).Returns(missingEndpoint);

        var transferredSlice = new TransferredSlice
        {
            Id = Guid.NewGuid(),
            CertificateId = Guid.NewGuid(),
            Quantity = _fixture.Create<int>(),
            ExternalEndpointId = externalEndpoint.Id,
            ExternalEndpointPosition = _fixture.Create<int>(),
            RandomR = _fixture.Create<byte[]>(),
            RegistryName = _fixture.Create<string>(),
            State = TransferredSliceState.Registering
        };
        _transferRepository.GetTransferredSlice(Arg.Any<Guid>()).Returns(transferredSlice);

        _context.Arguments.Returns(new SendInformationToReceiverWalletArgument()
        {
            ExternalEndpointId = externalEndpoint.Id,
            SliceId = transferredSlice.Id,
            WalletAttributes = [],
            RequestId = Guid.NewGuid(),
            Owner = owner
        });

        var fault = Substitute.For<MassTransit.ExecutionResult>();

        _context.Faulted(Arg.Any<Exception>()).Returns(fault);

        // Act
        var result = await _activity.Execute(_context);

        // Assert
        await _transferRepository.Received(0).SetTransferredSliceState(Arg.Any<Guid>(), TransferredSliceState.Transferred);
        result.Should().Be(fault);
    }
}
