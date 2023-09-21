using System;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Google.Protobuf;
using MassTransit;
using NSubstitute;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.IntegrationTests.TestExtensions;
using ProjectOrigin.WalletSystem.Server;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class SplitTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly Fixture _fixture;
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly string _registryName;
    private readonly IUnitOfWork _unitOfWork;
    private readonly RegistryProcessBuilder _processBuilder;

    public SplitTests(PostgresDatabaseFixture postgresDatabaseFixture)
    {
        _fixture = new Fixture();
        _dbFixture = postgresDatabaseFixture;
        _registryName = _fixture.Create<string>();
        _unitOfWork = _dbFixture.CreateUnitOfWork();
        _processBuilder = new RegistryProcessBuilder(
            _unitOfWork,
            Substitute.For<IEndpointNameFormatter>(),
            Guid.NewGuid()
        );
    }

    [Fact]
    public async Task TestSplitMethod()
    {
        // Arrange
        var depositEndpoint = await _dbFixture.CreateWalletDepositEndpoint(_fixture.Create<string>());

        var cert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _registryName, Server.Models.GranularCertificateType.Production);
        var secret = new SecretCommitmentInfo(150);
        var sourceSlice = await _dbFixture.CreateSlice(depositEndpoint, cert, secret);
        var publicKey = depositEndpoint.PublicKey.Derive(sourceSlice.DepositEndpointPosition).GetPublicKey();

        // Act
        var (newSlice1, newSlice2) = await _processBuilder.SplitSlice(sourceSlice, 100);
        var slip = _processBuilder.Build();

        // Assert
        slip.Should().NotBeNull();

        var commitmentInfo1 = new SecretCommitmentInfo((uint)newSlice1.Quantity, newSlice1.RandomR);
        var commitmentInfo2 = new SecretCommitmentInfo((uint)newSlice2.Quantity, newSlice2.RandomR);

        var (transaction, _) = slip.Itinerary[0].ShouldBeTransactionWithEvent<SlicedEvent>(
            transaction =>
                transaction.Header.FederatedStreamId.Registry == _registryName &&
                transaction.Header.FederatedStreamId.StreamId.Value == cert.Id.ToString() &&
                publicKey.Verify(transaction.Header.ToByteArray(), transaction.HeaderSignature.ToByteArray()),
            payload =>
                payload.NewSlices.Count == 2 &&
                payload.NewSlices[0].Quantity.IsEqual(commitmentInfo1) &&
                payload.NewSlices[1].Quantity.IsEqual(commitmentInfo2)
        );
        slip.Itinerary[1].ShouldWaitFor(transaction);

        slip.Itinerary[2].ShouldSetStates(new(){
            { newSlice1.Id, SliceState.Reserved },
            { newSlice2.Id, SliceState.Reserved },
            { sourceSlice.Id, SliceState.Sliced }
        });

        slip.Itinerary.Count.Should().Be(3);

        (await _unitOfWork.CertificateRepository.GetSlice(sourceSlice.Id)).SliceState.Should().Be(SliceState.Slicing);
        (await _unitOfWork.CertificateRepository.GetSlice(newSlice1.Id)).SliceState.Should().Be(SliceState.Registering);
        (await _unitOfWork.CertificateRepository.GetSlice(newSlice2.Id)).SliceState.Should().Be(SliceState.Registering);
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(100, 200)]
    public async Task TestSplitMethodWithQuantityEqualToSliceQuantity(uint sliceSize, int splitQuanity)
    {
        // Arrange
        var depositEndpoint = await _dbFixture.CreateWalletDepositEndpoint(_fixture.Create<string>());

        var cert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _registryName, Server.Models.GranularCertificateType.Production);
        var secret = new SecretCommitmentInfo(sliceSize);
        var slice = await _dbFixture.CreateSlice(depositEndpoint, cert, secret);

        // Act
        Func<Task> act = async () => await _processBuilder.SplitSlice(slice, splitQuanity);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Cannot split slice with quantity less than or equal to the requested quantity");
    }
}
