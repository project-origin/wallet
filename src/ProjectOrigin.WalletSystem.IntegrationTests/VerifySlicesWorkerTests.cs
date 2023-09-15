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

public class VerifySlicesWorkerTests
{
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
