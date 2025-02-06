using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ProjectOrigin.Vault.Activities;
using ProjectOrigin.Vault.CommandHandlers;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Exceptions;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Models;
using Xunit;

namespace ProjectOrigin.Vault.Tests.CommandHandlers;

public class ClaimCertificatesCommandHandlerTests
{
    private readonly Fixture _fixture;
    private readonly string _registryName;
    private readonly string _owner;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRegistryProcessBuilder _processBuilder;
    private readonly IClaimMetrics _claimMetrics;
    private readonly ClaimCertificateCommandHandler _commandHandler;
    private readonly ConsumeContext<ClaimCertificateCommand> _context;

    public ClaimCertificatesCommandHandlerTests()
    {
        _fixture = new Fixture();
        _registryName = _fixture.Create<string>();
        _owner = _fixture.Create<string>();

        _processBuilder = Substitute.For<IRegistryProcessBuilder>();
        _claimMetrics = Substitute.For<IClaimMetrics>();
        var _processBuilderFactory = Substitute.For<IRegistryProcessBuilderFactory>();
        _processBuilderFactory.Create(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<IUnitOfWork>()).Returns(_processBuilder);

        _unitOfWork = Substitute.For<IUnitOfWork>();

        _commandHandler = new ClaimCertificateCommandHandler(
            Substitute.For<ILogger<ClaimCertificateCommandHandler>>(),
            _unitOfWork,
            _processBuilderFactory,
            _claimMetrics
        );
        _context = Substitute.For<ConsumeContext<ClaimCertificateCommand>>();
    }

    [Fact]
    public async Task MatchingSlices()
    {
        // arrange
        var (consSlices, prodSlices) = SetupBase(125u, new uint[] { 125u }, new uint[] { 125u });

        // act
        await _commandHandler.Consume(_context);

        // assert
        await _processBuilder.Received(1).Claim(Arg.Is(prodSlices.Single()), Arg.Is(consSlices.Single()));
        _processBuilder.Received(1).Build();
        _processBuilder.ReceivedCalls().Count().Should().Be(2);
    }

    [Fact]
    public async Task ReserveQuantityThrowsQuantityNotYetAvailableToReserveException_Throws()
    {
        // arrange
        var command = new ClaimCertificateCommand
        {
            Owner = _owner,
            ClaimId = Guid.NewGuid(),
            ConsumptionRegistry = _registryName,
            ConsumptionCertificateId = Guid.NewGuid(),
            ProductionRegistry = _registryName,
            ProductionCertificateId = Guid.NewGuid(),
            Quantity = 123
        };
        _context.Message.Returns(command);
        _unitOfWork.CertificateRepository.ReserveQuantity(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<uint>())
            .ThrowsAsync(_ => throw new QuantityNotYetAvailableToReserveException("Owner has enough quantity, but it is not yet available to reserve"));

        // act
        var sut = () => _commandHandler.Consume(_context);

        await sut.Should().ThrowAsync<QuantityNotYetAvailableToReserveException>();
    }

    [Fact]
    public async Task SingleConsumption_TwoProduction_NoRemainder()
    {
        // arrange
        var (consSlices, prodSlices) = SetupBase(125u, new uint[] { 125u }, new uint[] { 80u, 45u });
        var (c1, c2) = SetupSplit(consSlices[0], 80L);

        // act
        await _commandHandler.Consume(_context);

        // assert
        await _processBuilder.Received(1).SplitSlice(Arg.Is(consSlices[0]), Arg.Is(80L), Arg.Any<RequestStatusArgs>());
        await _processBuilder.Received(1).Claim(Arg.Is(prodSlices[0]), Arg.Is(c1));
        await _processBuilder.Received(1).Claim(Arg.Is(prodSlices[1]), Arg.Is(c2));
        _processBuilder.Received(1).Build();
        _processBuilder.ReceivedCalls().Count().Should().Be(4);
    }

    [Fact]
    public async Task TwoConsumption_SingleProduction_NoRemainder()
    {
        // arrange
        var (consSlices, prodSlices) = SetupBase(125u, new uint[] { 75u, 50u }, new uint[] { 125u });
        var (p1, p2) = SetupSplit(prodSlices[0], 75L);

        // act
        await _commandHandler.Consume(_context);

        // assert
        await _processBuilder.Received(1).SplitSlice(Arg.Is(prodSlices[0]), Arg.Is(75L), Arg.Any<RequestStatusArgs>());
        await _processBuilder.Received(1).Claim(Arg.Is(p1), Arg.Is(consSlices[0]));
        await _processBuilder.Received(1).Claim(Arg.Is(p2), Arg.Is(consSlices[1]));
        _processBuilder.Received(1).Build();
        _processBuilder.ReceivedCalls().Count().Should().Be(4);
    }

    [Fact]
    public async Task TwoConsumption_TwoProduction_UnequalSizes_NoRemainder()
    {
        // arrange
        var (consSlices, prodSlices) = SetupBase(125u, new uint[] { 75u, 50u }, new uint[] { 65u, 60u });
        var (c1, c2) = SetupSplit(consSlices[0], 65L);
        var (p1, p2) = SetupSplit(prodSlices[1], 10L);

        // act
        await _commandHandler.Consume(_context);

        // assert
        await _processBuilder.Received(1).SplitSlice(Arg.Is(consSlices[0]), Arg.Is(65L), Arg.Any<RequestStatusArgs>());
        await _processBuilder.Received(1).SplitSlice(Arg.Is(prodSlices[1]), Arg.Is(10L), Arg.Any<RequestStatusArgs>());
        await _processBuilder.Received(1).Claim(Arg.Is(prodSlices[0]), Arg.Is(c1));
        await _processBuilder.Received(1).Claim(Arg.Is(p1), Arg.Is(c2));
        await _processBuilder.Received(1).Claim(Arg.Is(p2), Arg.Is(consSlices[1]));
        _processBuilder.Received(1).Build();
        _processBuilder.ReceivedCalls().Count().Should().Be(6);
    }

    [Fact]
    public async Task OneConsumption_OneProduction_ConsumptionWithRemainder()
    {
        // arrange
        var (consSlices, prodSlices) = SetupBase(125u, new uint[] { 150u }, new uint[] { 125u });
        var (c1, c2) = SetupSplit(consSlices[0], 125L);

        // act
        await _commandHandler.Consume(_context);

        // assert
        await _processBuilder.Received(1).SplitSlice(Arg.Is(consSlices[0]), Arg.Is(125L), Arg.Any<RequestStatusArgs>());
        await _processBuilder.Received(1).Claim(Arg.Is(prodSlices[0]), Arg.Is(c1));
        _processBuilder.Received(1).Build();
        _processBuilder.Received(1).SetWalletSliceStates(Arg.Is<Dictionary<Guid, WalletSliceState>>(x => x.SequenceEqual(new Dictionary<Guid, WalletSliceState>
        {
            { c2.Id, WalletSliceState.Available }
        })), Arg.Any<RequestStatusArgs>());
        _processBuilder.ReceivedCalls().Count().Should().Be(4);
    }

    [Fact]
    public async Task OneConsumption_ThreeProduction_ConsumptionWithRemainder()
    {
        // arrange
        var (consSlices, prodSlices) = SetupBase(100u, new uint[] { 150u }, new uint[] { 50u, 25u, 25u });
        var (c1, cRemainder) = SetupSplit(consSlices[0], 100L);
        var (c2, c3) = SetupSplit(c1, 50L);
        var (c4, c5) = SetupSplit(c3, 25L);

        // act
        await _commandHandler.Consume(_context);

        // assert
        await _processBuilder.Received(1).SplitSlice(Arg.Is(consSlices[0]), Arg.Is(100L), Arg.Any<RequestStatusArgs>());
        await _processBuilder.Received(1).SplitSlice(Arg.Is(c1), Arg.Is(50L), Arg.Any<RequestStatusArgs>());
        await _processBuilder.Received(1).SplitSlice(Arg.Is(c3), Arg.Is(25L), Arg.Any<RequestStatusArgs>());
        await _processBuilder.Received(1).Claim(Arg.Is(prodSlices[0]), Arg.Is(c2));
        await _processBuilder.Received(1).Claim(Arg.Is(prodSlices[1]), Arg.Is(c4));
        await _processBuilder.Received(1).Claim(Arg.Is(prodSlices[2]), Arg.Is(c5));
        _processBuilder.Received(1).Build();
        _processBuilder.Received(1).SetWalletSliceStates(Arg.Is<Dictionary<Guid, WalletSliceState>>(x => x.SequenceEqual(new Dictionary<Guid, WalletSliceState>
        {
            { cRemainder.Id, WalletSliceState.Available }
        })), Arg.Any<RequestStatusArgs>());

        _processBuilder.ReceivedCalls().Count().Should().Be(8);
    }

    [Fact]
    public async Task ThreeConsumption_OneProduction_ProductionWithRemainder()
    {
        // arrange
        var (consSlices, prodSlices) = SetupBase(100u, new uint[] { 50u, 25u, 25u }, new uint[] { 150u });
        var (p1, p2) = SetupSplit(prodSlices[0], 50L);
        var (p3, p4) = SetupSplit(p2, 25L);
        var (p5, p6) = SetupSplit(p4, 25L);

        // act
        await _commandHandler.Consume(_context);

        // assert
        await _processBuilder.Received(1).SplitSlice(Arg.Is(prodSlices[0]), Arg.Is(50L), Arg.Any<RequestStatusArgs>());
        await _processBuilder.Received(1).SplitSlice(Arg.Is(p2), Arg.Is(25L), Arg.Any<RequestStatusArgs>());
        await _processBuilder.Received(1).SplitSlice(Arg.Is(p4), Arg.Is(25L), Arg.Any<RequestStatusArgs>());
        await _processBuilder.Received(1).Claim(Arg.Is(p1), Arg.Is(consSlices[0]));
        await _processBuilder.Received(1).Claim(Arg.Is(p3), Arg.Is(consSlices[1]));
        await _processBuilder.Received(1).Claim(Arg.Is(p5), Arg.Is(consSlices[2]));
        _processBuilder.Received(1).Build();
        _processBuilder.Received(1).SetWalletSliceStates(Arg.Is<Dictionary<Guid, WalletSliceState>>(x => x.SequenceEqual(new Dictionary<Guid, WalletSliceState>
        {
            { p6.Id, WalletSliceState.Available }
        })), Arg.Any<RequestStatusArgs>());

        _processBuilder.ReceivedCalls().Count().Should().Be(8);
    }

    /// <summary>
    /// Setup the basics for tests.
    /// </summary>
    /// <param name="quantity">The quantity used on the ClaimCertificateCommand</param>
    /// <param name="consumptionQuantities">The list of sizes of the reserved consumption slices returned from the repository</param>
    /// <param name="productionQuantities">The list of sizes of the reserved production slices returned from the repository</param>
    /// <returns>Two lists of slices</returns>
    private (IList<WalletSlice> consumptionSlices, IList<WalletSlice> productionSlices) SetupBase(uint quantity, uint[] consumptionQuantities, uint[] productionQuantities)
    {
        var command = new ClaimCertificateCommand
        {
            Owner = _owner,
            ClaimId = Guid.NewGuid(),
            ConsumptionRegistry = _registryName,
            ConsumptionCertificateId = Guid.NewGuid(),
            ProductionRegistry = _registryName,
            ProductionCertificateId = Guid.NewGuid(),
            Quantity = quantity
        };
        _context.Message.Returns(command);

        List<WalletSlice> conSlices = SetupReserveQuantity(command.ConsumptionRegistry, command.ConsumptionCertificateId, consumptionQuantities);
        List<WalletSlice> prodSlices = SetupReserveQuantity(command.ProductionRegistry, command.ProductionCertificateId, productionQuantities);

        return (conSlices.ToList(), prodSlices.ToList());
    }

    private List<WalletSlice> SetupReserveQuantity(string registry, Guid certId, uint[] consumptionQuantities)
    {
        var conSlices = consumptionQuantities.Select(q => _fixture.Create<WalletSlice>() with
        {
            RegistryName = registry,
            CertificateId = certId,
            Quantity = q,
        }).ToList();

        _unitOfWork.CertificateRepository.ReserveQuantity(
            Arg.Any<string>(),
            Arg.Is(registry),
            Arg.Is(certId),
            Arg.Any<uint>())
            .Returns(conSlices);
        return conSlices;
    }

    private (WalletSlice, WalletSlice) SetupSplit(WalletSlice slice, long quantity)
    {
        var s1 = slice with
        {
            Id = Guid.NewGuid(),
            Quantity = quantity
        };
        var s2 = slice with
        {
            Id = Guid.NewGuid(),
            Quantity = slice.Quantity - quantity
        };
        _processBuilder.SplitSlice(Arg.Is(slice), Arg.Is(quantity), Arg.Any<RequestStatusArgs>()).Returns((s1, s2));

        return (s1, s2);
    }
}
