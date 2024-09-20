using System;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Google.Protobuf;
using MassTransit;
using NSubstitute;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using ProjectOrigin.Vault.Tests.TestExtensions;
using ProjectOrigin.Vault.Activities;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Models;
using Xunit;

namespace ProjectOrigin.Vault.Tests;

public class ClaimTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly Fixture _fixture;
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly string _registryName;
    private readonly IUnitOfWork _unitOfWork;
    private readonly RegistryProcessBuilder _processBuilder;

    public ClaimTests(PostgresDatabaseFixture postgresDatabaseFixture)
    {
        _fixture = new Fixture();
        _dbFixture = postgresDatabaseFixture;
        _registryName = _fixture.Create<string>();
        _unitOfWork = _dbFixture.CreateUnitOfWork();
        _processBuilder = new RegistryProcessBuilder(
            _unitOfWork,
            Substitute.For<IEndpointNameFormatter>(),
            Guid.NewGuid(),
            _fixture.Create<string>()
        );
    }

    [Fact]
    public async Task TestClaimEqualSize()
    {
        // Arrange
        var endpoint = await _dbFixture.CreateWalletEndpoint(_fixture.Create<string>());

        var prodCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _registryName, Models.GranularCertificateType.Production);
        var prodSecret = new SecretCommitmentInfo(150);
        var prodSlice = await _dbFixture.CreateSlice(endpoint, prodCert, prodSecret);
        var prodPublicKey = endpoint.PublicKey.Derive(prodSlice.WalletEndpointPosition);

        var consCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _registryName, Models.GranularCertificateType.Consumption);
        var consSecret = new SecretCommitmentInfo(150);
        var consSlice = await _dbFixture.CreateSlice(endpoint, consCert, consSecret);
        var consPublicKey = endpoint.PublicKey.Derive(consSlice.WalletEndpointPosition);

        // Act
        await _processBuilder.Claim(prodSlice, consSlice);
        var slip = _processBuilder.Build();

        // Assert
        slip.Should().NotBeNull();

        var (t1, a1) = slip.Itinerary[0].ShouldBeTransactionWithEvent<AllocatedEvent>(
            transaction =>
                transaction.Header.FederatedStreamId.Registry == _registryName &&
                transaction.Header.FederatedStreamId.StreamId.Value == prodCert.Id.ToString() &&
                prodPublicKey.Verify(transaction.Header.ToByteArray(), transaction.HeaderSignature.ToByteArray()),
            payload =>
                payload.ConsumptionCertificateId.Registry == _registryName &&
                payload.ConsumptionCertificateId.StreamId.Value == consCert.Id.ToString() &&
                payload.ProductionCertificateId.Registry == _registryName &&
                payload.ProductionCertificateId.StreamId.Value == prodCert.Id.ToString()
        );
        slip.Itinerary[1].ShouldWaitFor(t1);

        var (t2, _) = slip.Itinerary[2].ShouldBeTransactionWithEvent<AllocatedEvent>(
            transaction =>
                transaction.Header.FederatedStreamId.Registry == _registryName &&
                transaction.Header.FederatedStreamId.StreamId.Value == consCert.Id.ToString() &&
                consPublicKey.Verify(transaction.Header.ToByteArray(), transaction.HeaderSignature.ToByteArray()),
            payload =>
                payload.ConsumptionCertificateId.Registry == _registryName &&
                payload.ConsumptionCertificateId.StreamId.Value == consCert.Id.ToString() &&
                payload.ProductionCertificateId.Registry == _registryName &&
                payload.ProductionCertificateId.StreamId.Value == prodCert.Id.ToString() &&
                payload.AllocationId.Value == a1.AllocationId.Value
        );
        slip.Itinerary[3].ShouldWaitFor(t2);

        var (t3, _) = slip.Itinerary[4].ShouldBeTransactionWithEvent<ClaimedEvent>(
            transaction =>
                transaction.Header.FederatedStreamId.Registry == _registryName &&
                transaction.Header.FederatedStreamId.StreamId.Value == prodCert.Id.ToString() &&
                prodPublicKey.Verify(transaction.Header.ToByteArray(), transaction.HeaderSignature.ToByteArray()),
            payload =>
                payload.CertificateId.Registry == _registryName &&
                payload.CertificateId.StreamId.Value == prodCert.Id.ToString() &&
                payload.AllocationId.Value == a1.AllocationId.Value
        );
        slip.Itinerary[5].ShouldWaitFor(t3);

        var (t4, _) = slip.Itinerary[6].ShouldBeTransactionWithEvent<ClaimedEvent>(
            transaction =>
                transaction.Header.FederatedStreamId.Registry == _registryName &&
                transaction.Header.FederatedStreamId.StreamId.Value == consCert.Id.ToString() &&
                consPublicKey.Verify(transaction.Header.ToByteArray(), transaction.HeaderSignature.ToByteArray()),
            payload =>
                payload.CertificateId.Registry == _registryName &&
                payload.CertificateId.StreamId.Value == consCert.Id.ToString() &&
                payload.AllocationId.Value == a1.AllocationId.Value
        );
        slip.Itinerary[7].ShouldWaitFor(t4);

        slip.Itinerary[8].ShouldSetStates(new(){
            { prodSlice.Id, WalletSliceState.Claimed },
            { consSlice.Id, WalletSliceState.Claimed },
        });

        slip.Itinerary[9].ShouldBeActivity<UpdateClaimStateActivity, UpdateClaimStateArguments>()
            .Should().Match<UpdateClaimStateArguments>(x =>
                x.Id.ToString() == a1.AllocationId.Value &&
                x.State == ClaimState.Claimed);

        slip.Itinerary.Count.Should().Be(10);

        Claim claim = await _unitOfWork.ClaimRepository.GetClaim(Guid.Parse(a1.AllocationId.Value));
        claim.ConsumptionSliceId.Should().Be(consSlice.Id);
        claim.ProductionSliceId.Should().Be(prodSlice.Id);
        claim.State.Should().Be(ClaimState.Created);
    }

    [Theory]
    [InlineData(200, 100)]
    [InlineData(100, 200)]
    public async Task TestClaimUnqualSize(uint prodSize, uint consSize)
    {
        // Arrange
        var endpoint = await _dbFixture.CreateWalletEndpoint(_fixture.Create<string>());

        var prodCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _registryName, Models.GranularCertificateType.Production);
        var prodSecret = new SecretCommitmentInfo(prodSize);
        var prodSlice = await _dbFixture.CreateSlice(endpoint, prodCert, prodSecret);

        var consCert = await _dbFixture.CreateCertificate(Guid.NewGuid(), _registryName, Models.GranularCertificateType.Consumption);
        var consSecret = new SecretCommitmentInfo(consSize);
        var consSlice = await _dbFixture.CreateSlice(endpoint, consCert, consSecret);

        // Act
        var method = () => _processBuilder.Claim(prodSlice, consSlice);

        // Assert
        await method.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Production and consumption slices must have the same quantity");
    }
}
