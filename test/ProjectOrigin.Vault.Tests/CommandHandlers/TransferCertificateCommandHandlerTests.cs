using System;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ProjectOrigin.Vault.CommandHandlers;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Exceptions;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Models;
using Xunit;

namespace ProjectOrigin.Vault.Tests.CommandHandlers;
public class TransferCertificateCommandHandlerTests
{
    private readonly Fixture _fixture;
    private readonly string _registryName;
    private readonly string _owner;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TransferCertificateCommandHandler _commandHandler;
    private readonly ConsumeContext<TransferCertificateCommand> _context;
    private readonly ITransferMetrics _transferMetrics;

    public TransferCertificateCommandHandlerTests()
    {
        _fixture = new Fixture();
        _registryName = _fixture.Create<string>();
        _owner = _fixture.Create<string>();

        _unitOfWork = Substitute.For<IUnitOfWork>();
        _transferMetrics = Substitute.For<ITransferMetrics>();

        _commandHandler = new TransferCertificateCommandHandler(
            _unitOfWork,
            Substitute.For<ILogger<TransferCertificateCommandHandler>>(),
            Substitute.For<IEndpointNameFormatter>(),
            _transferMetrics
        );
        _context = Substitute.For<ConsumeContext<TransferCertificateCommand>>();
    }

    [Fact]
    public async Task ReserveQuantityThrowsQuantityNotYetAvailableToReserveException_Throws()
    {
        var command = new TransferCertificateCommand
        {
            CertificateId = Guid.NewGuid(),
            Quantity = 1,
            Registry = _registryName,
            Owner = _owner,
            HashedAttributes = new[] { "AssetId" },
            Receiver = Guid.NewGuid(),
            TransferRequestId = Guid.NewGuid()
        };
        _context.Message.Returns(command);
        _unitOfWork.WalletRepository.GetExternalEndpoint(Arg.Any<Guid>())
            .Returns(new ExternalEndpoint { Endpoint = "http://localhost:5000", Id = Guid.NewGuid(), Owner = _owner, ReferenceText = "", PublicKey = null! });
        _unitOfWork.CertificateRepository.ReserveQuantity(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<uint>())
            .ThrowsAsync(_ => throw new QuantityNotYetAvailableToReserveException("Owner has enough quantity, but it is not yet available to reserve"));

        var sut = () => _commandHandler.Consume(_context);

        await sut.Should().ThrowAsync<QuantityNotYetAvailableToReserveException>();
    }
}
