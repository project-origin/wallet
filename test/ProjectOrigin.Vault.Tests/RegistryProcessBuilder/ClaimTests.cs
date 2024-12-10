using System;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Google.Protobuf;
using MassTransit;
using NSubstitute;
using MsOptions = Microsoft.Extensions.Options;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using ProjectOrigin.Vault.Tests.TestExtensions;
using ProjectOrigin.Vault.Activities;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Models;
using Xunit;
using ProjectOrigin.Vault.Options;
using System.Collections.Generic;
using System.Linq;

namespace ProjectOrigin.Vault.Tests;

public class ClaimTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly Fixture _fixture;
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly string _registryName;
    private readonly IUnitOfWork _unitOfWork;

    public ClaimTests(PostgresDatabaseFixture postgresDatabaseFixture)
    {
        _fixture = new Fixture();
        _dbFixture = postgresDatabaseFixture;
        _registryName = _fixture.Create<string>();
        _unitOfWork = _dbFixture.CreateUnitOfWork();
    }

    [Fact]
    public async Task TestClaimEqualSize_WithoutChronicler()
    {
        // Arrange
        var _processBuilder = new RegistryProcessBuilder(
            _unitOfWork,
            Substitute.For<IEndpointNameFormatter>(),
            Guid.NewGuid(),
            MsOptions.Options.Create(new NetworkOptions()
            {
                Areas = new Dictionary<string, AreaInfo>(){
                    {
                        PostgresFixtureExtensions.Area, new AreaInfo(){
                            Chronicler = null,
                            IssuerKeys = new List<KeyInfo>(){}
                        }
                    }}
            }),
            _fixture.Create<string>());

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

        slip.Itinerary[0].ShouldBeActivity<AllocateActivity, AllocateArguments>()
            .Should().Match<AllocateArguments>(x =>
                x.AllocationId != Guid.Empty &&
                x.ProductionSliceId == prodSlice.Id &&
                x.ConsumptionSliceId == consSlice.Id &&
                x.ChroniclerRequestId == null &&
                x.CertificateId.Registry == prodCert.RegistryName &&
                x.CertificateId.StreamId.Value == prodCert.Id.ToString() &&
                x.RequestId != Guid.Empty &&
                x.Owner != string.Empty);

        var allocationId = slip.Itinerary[0].ShouldBeActivity<AllocateActivity, AllocateArguments>().AllocationId.ToString();

        slip.Itinerary[1].ShouldBeActivity<AllocateActivity, AllocateArguments>()
            .Should().Match<AllocateArguments>(x =>
                x.AllocationId != Guid.Empty &&
                x.ProductionSliceId == prodSlice.Id &&
                x.ConsumptionSliceId == consSlice.Id &&
                x.ChroniclerRequestId == null &&
                x.CertificateId.Registry == consCert.RegistryName &&
                x.CertificateId.StreamId.Value == consCert.Id.ToString() &&
                x.RequestId != Guid.Empty &&
                x.Owner != string.Empty);

        var (t3, _) = slip.Itinerary[2].ShouldBeTransactionWithEvent<ClaimedEvent>(
            transaction =>
                transaction.Header.FederatedStreamId.Registry == _registryName &&
                transaction.Header.FederatedStreamId.StreamId.Value == prodCert.Id.ToString() &&
                prodPublicKey.Verify(transaction.Header.ToByteArray(), transaction.HeaderSignature.ToByteArray()),
            payload =>
                payload.CertificateId.Registry == _registryName &&
                payload.CertificateId.StreamId.Value == prodCert.Id.ToString() &&
                payload.AllocationId.Value == allocationId
        );
        slip.Itinerary[3].ShouldWaitFor(t3);

        var (t4, _) = slip.Itinerary[4].ShouldBeTransactionWithEvent<ClaimedEvent>(
            transaction =>
                transaction.Header.FederatedStreamId.Registry == _registryName &&
                transaction.Header.FederatedStreamId.StreamId.Value == consCert.Id.ToString() &&
                consPublicKey.Verify(transaction.Header.ToByteArray(), transaction.HeaderSignature.ToByteArray()),
            payload =>
                payload.CertificateId.Registry == _registryName &&
                payload.CertificateId.StreamId.Value == consCert.Id.ToString() &&
                payload.AllocationId.Value == allocationId
        );
        slip.Itinerary[5].ShouldWaitFor(t4);

        slip.Itinerary[6].ShouldSetStates(new(){
            { prodSlice.Id, WalletSliceState.Claimed },
            { consSlice.Id, WalletSliceState.Claimed },
        });

        slip.Itinerary[7].ShouldBeActivity<UpdateClaimStateActivity, UpdateClaimStateArguments>()
            .Should().Match<UpdateClaimStateArguments>(x =>
                x.Id.ToString() == allocationId &&
                x.State == ClaimState.Claimed);

        slip.Itinerary.Count.Should().Be(8);

        Claim claim = await _unitOfWork.ClaimRepository.GetClaim(Guid.Parse(allocationId));
        claim.ConsumptionSliceId.Should().Be(consSlice.Id);
        claim.ProductionSliceId.Should().Be(prodSlice.Id);
        claim.State.Should().Be(ClaimState.Created);
    }


    [Fact]
    public async Task TestClaimEqualSize_WithChronicler()
    {
        // Arrange
        var _processBuilder = new RegistryProcessBuilder(
            _unitOfWork,
            Substitute.For<IEndpointNameFormatter>(),
            Guid.NewGuid(),
            MsOptions.Options.Create(new NetworkOptions()
            {
                Areas = new Dictionary<string, AreaInfo>(){
                    {
                        PostgresFixtureExtensions.Area, new AreaInfo(){
                            Chronicler = new ChroniclerInfo()
                            {
                                Url = "http://example.com:5000",
                                SignerKeys = new List<KeyInfo>()
                            },
                            IssuerKeys = new List<KeyInfo>()
                        }
                    }}
            }),
            _fixture.Create<string>());

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

        slip.Itinerary[0].ShouldBeActivity<SendClaimIntentToChroniclerActivity, SendClaimIntentToChroniclerArgument>()
            .Should().Match<SendClaimIntentToChroniclerArgument>(x =>
                x.Id != Guid.Empty &&
                x.CertificateId.Registry == prodCert.RegistryName &&
                x.CertificateId.StreamId.Value == prodCert.Id.ToString() &&
                x.Quantity == prodSlice.Quantity &&
                x.RandomR.ToArray().SequenceEqual(prodSlice.RandomR));
        var chronId = slip.Itinerary[0].ShouldBeActivity<SendClaimIntentToChroniclerActivity, SendClaimIntentToChroniclerArgument>().Id;

        slip.Itinerary[1].ShouldBeActivity<AllocateActivity, AllocateArguments>()
            .Should().Match<AllocateArguments>(x =>
                x.AllocationId != Guid.Empty &&
                x.ProductionSliceId == prodSlice.Id &&
                x.ConsumptionSliceId == consSlice.Id &&
                x.ChroniclerRequestId.Equals(chronId) &&
                x.CertificateId.Registry == prodCert.RegistryName &&
                x.CertificateId.StreamId.Value == prodCert.Id.ToString() &&
                x.RequestId != Guid.Empty &&
                x.Owner != string.Empty);

        var allocationId = slip.Itinerary[1].ShouldBeActivity<AllocateActivity, AllocateArguments>().AllocationId.ToString();


        slip.Itinerary[2].ShouldBeActivity<SendClaimIntentToChroniclerActivity, SendClaimIntentToChroniclerArgument>()
            .Should().Match<SendClaimIntentToChroniclerArgument>(x =>
                x.Id != Guid.Empty &&
                x.CertificateId.Registry == consCert.RegistryName &&
                x.CertificateId.StreamId.Value == consCert.Id.ToString() &&
                x.Quantity == consSlice.Quantity &&
                x.RandomR.ToArray().SequenceEqual(consSlice.RandomR));
        var chronId2 = slip.Itinerary[2].ShouldBeActivity<SendClaimIntentToChroniclerActivity, SendClaimIntentToChroniclerArgument>().Id;

        slip.Itinerary[3].ShouldBeActivity<AllocateActivity, AllocateArguments>()
            .Should().Match<AllocateArguments>(x =>
                x.AllocationId != Guid.Empty &&
                x.ProductionSliceId == prodSlice.Id &&
                x.ConsumptionSliceId == consSlice.Id &&
                x.ChroniclerRequestId.Equals(chronId2) &&
                x.CertificateId.Registry == consCert.RegistryName &&
                x.CertificateId.StreamId.Value == consCert.Id.ToString() &&
                x.RequestId != Guid.Empty &&
                x.Owner != string.Empty);

        var (t3, _) = slip.Itinerary[4].ShouldBeTransactionWithEvent<ClaimedEvent>(
            transaction =>
                transaction.Header.FederatedStreamId.Registry == _registryName &&
                transaction.Header.FederatedStreamId.StreamId.Value == prodCert.Id.ToString() &&
                prodPublicKey.Verify(transaction.Header.ToByteArray(), transaction.HeaderSignature.ToByteArray()),
            payload =>
                payload.CertificateId.Registry == _registryName &&
                payload.CertificateId.StreamId.Value == prodCert.Id.ToString() &&
                payload.AllocationId.Value == allocationId
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
                payload.AllocationId.Value == allocationId
        );
        slip.Itinerary[7].ShouldWaitFor(t4);

        slip.Itinerary[8].ShouldSetStates(new(){
            { prodSlice.Id, WalletSliceState.Claimed },
            { consSlice.Id, WalletSliceState.Claimed },
        });

        slip.Itinerary[9].ShouldBeActivity<UpdateClaimStateActivity, UpdateClaimStateArguments>()
            .Should().Match<UpdateClaimStateArguments>(x =>
                x.Id.ToString() == allocationId &&
                x.State == ClaimState.Claimed);

        slip.Itinerary.Count.Should().Be(10);

        Claim claim = await _unitOfWork.ClaimRepository.GetClaim(Guid.Parse(allocationId));
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
        var _processBuilder = new RegistryProcessBuilder(
            _unitOfWork,
            Substitute.For<IEndpointNameFormatter>(),
            Guid.NewGuid(),
            MsOptions.Options.Create(new NetworkOptions()
            {
                Areas = new Dictionary<string, AreaInfo>(){
                    {
                        PostgresFixtureExtensions.Area, new AreaInfo(){
                            Chronicler = null,
                            IssuerKeys = new List<KeyInfo>(){}
                        }
                    }}
            }),
            _fixture.Create<string>());

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
