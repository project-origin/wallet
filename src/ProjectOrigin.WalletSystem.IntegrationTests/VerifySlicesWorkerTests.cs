using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using ProjectOrigin.WalletSystem.Server.BackgroundJobs;
using ProjectOrigin.WalletSystem.Server.CommandHandlers;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class VerifySlicesWorkerTests : WalletSystemTestsBase, IClassFixture<InMemoryFixture>, IClassFixture<RegistryFixture>
{
    private RegistryFixture _registryFixture;
    private Fixture _fixture;

    public VerifySlicesWorkerTests(
        GrpcTestFixture<Startup> grpcFixture,
        PostgresDatabaseFixture dbFixture,
        InMemoryFixture inMemoryFixture,
        RegistryFixture registryFixture,
        ITestOutputHelper outputHelper)
        : base(
              grpcFixture,
              dbFixture,
              inMemoryFixture,
              outputHelper,
              registryFixture)
    {
        _registryFixture = registryFixture;
        _fixture = new Fixture();
    }

    [Fact]
    public async void WhenReceivedSliceIsValid_ExpectIsConvertedToSliceAndCertificateIsCreated()
    {
        var owner = _fixture.Create<string>();
        var depositEndpoint = await CreateWalletDepositEndpoint(owner);
        var commitment = new SecretCommitmentInfo(150);
        var position = 1;
        var publicKey = depositEndpoint.PublicKey.Derive(position).GetPublicKey();

        var issuedEvent = await _registryFixture.IssueCertificate(Electricity.V1.GranularCertificateType.Production, commitment, publicKey);
        var certId = Guid.Parse(issuedEvent.CertificateId.StreamId.Value);

        var receivedSlice = await CreateReceivedSlice(depositEndpoint, position, issuedEvent.CertificateId.Registry, certId, commitment.Message, commitment.BlindingValue.ToArray());

        await using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var cert = await connection.RepeatedlyQueryFirstOrDefaultUntil<Certificate>("SELECT * FROM Certificates WHERE id = @id", new { id = certId });
            var slice = await connection.RepeatedlyQueryFirstOrDefaultUntil<Slice>("SELECT * FROM Slices WHERE certificateId = @id", new { id = certId });
            cert.Should().NotBeNull();
            cert.Id.Should().Be(receivedSlice.CertificateId);
            slice.Should().NotBeNull();
            slice.CertificateId.Should().Be(receivedSlice.CertificateId);
            slice.Quantity.Should().Be(commitment.Message);
        }
    }

    [Fact]
    public async void WhenReceivedSliceIsUnknownInRegistry_ExpectReceivedSliceDeleted()
    {
        var owner = _fixture.Create<string>();
        var depositEndpoint = await CreateWalletDepositEndpoint(owner);
        var commitment = new SecretCommitmentInfo(150);
        var position = 1;

        var certId = Guid.NewGuid();
        var registryName = _registryFixture.Name;

        await CreateReceivedSlice(depositEndpoint, position, registryName, certId, commitment.Message, commitment.BlindingValue.ToArray());

        await using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var rSlice = await connection.RepeatedlyQueryUntilNull<ReceivedSlice>("SELECT * FROM ReceivedSlices WHERE certificateId = @id", new { id = certId });
            rSlice.Should().BeNull();

            var cert = await connection.QueryAsync<Certificate>("SELECT * FROM Certificates WHERE id = @id", new { id = certId });
            cert.Should().BeEmpty();
        }
    }

    [Fact]
    public async void WhenReceivedSliceIsUnknownRegistry_ExpectReceivedSliceDeleted()
    {
        var owner = _fixture.Create<string>();
        var depositEndpoint = await CreateWalletDepositEndpoint(owner);
        var commitment = new SecretCommitmentInfo(150);
        var position = 1;

        var certId = Guid.NewGuid();
        var registryName = _fixture.Create<string>();

        await CreateReceivedSlice(depositEndpoint, position, registryName, certId, commitment.Message, commitment.BlindingValue.ToArray());

        await using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var rSlice = await connection.RepeatedlyQueryUntilNull<ReceivedSlice>("SELECT * FROM ReceivedSlices WHERE certificateId = @id", new { id = certId });
            rSlice.Should().BeNull();

            var cert = await connection.QueryAsync<Certificate>("SELECT * FROM Certificates WHERE id = @id", new { id = certId });
            cert.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task WhenReceivedSliceIsValid_ExpectIsConvertedToSliceAndCertificateIsCreated()
    {
        // Arrange
        var fixture = new Fixture();
        var fakeUnitOfWork = Substitute.For<IUnitOfWork>();
        var fakeBus = Substitute.For<IBus>();

        VerifySlicesWorker worker = new VerifySlicesWorker(
            Substitute.For<ILogger<VerifySlicesWorker>>(),
            fakeUnitOfWork,
            fakeBus);

        var fakeRecevedSlice = fixture.Create<ReceivedSlice>();
        int callCount = 0;

        fakeUnitOfWork.CertificateRepository.GetTop1ReceivedSlice().Returns(_ => callCount++ > 0 ? null : fakeRecevedSlice);

        // Act
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(500);

        // Assert
        await fakeUnitOfWork.CertificateRepository.Received(2).GetTop1ReceivedSlice();
        await fakeUnitOfWork.CertificateRepository.Received(1).RemoveReceivedSlice(fakeRecevedSlice);
        fakeUnitOfWork.Received(1).Commit();
        await fakeBus.Received(1).Publish(Arg.Is<VerifySliceCommand>(command =>
            command.Id == fakeRecevedSlice.Id &&
            command.DepositEndpointId == fakeRecevedSlice.DepositEndpointId &&
            command.DepositEndpointPosition == fakeRecevedSlice.DepositEndpointPosition &&
            command.Registry == fakeRecevedSlice.Registry &&
            command.CertificateId == fakeRecevedSlice.CertificateId &&
            command.Quantity == fakeRecevedSlice.Quantity &&
            command.RandomR == fakeRecevedSlice.RandomR));
    }
}
