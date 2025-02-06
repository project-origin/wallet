using System;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using ProjectOrigin.Vault.Activities;
using ProjectOrigin.Vault.Options;
using Microsoft.Extensions.Logging;
using ProjectOrigin.PedersenCommitment;
using MassTransit.Courier.Contracts;
using Moq;
using ProjectOrigin.Chronicler.V1;
using Grpc.Core;
using Google.Protobuf;
using AutoFixture;
using Microsoft.Extensions.Options;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using FluentAssertions;
using ProjectOrigin.Vault.Metrics;

namespace ProjectOrigin.Vault.Tests;

public class ChroniclerExecutionTest
{
    [Fact]
    public async Task ChroniclerAndAllocate_ShouldRevise()
    {
        // Arrange
        var algorithm = new Secp256k1Algorithm();
        var fixture = new Fixture();
        var registryName = fixture.Create<string>();
        var certId = new Common.V1.FederatedStreamId()
        {
            Registry = registryName,
            StreamId = new Common.V1.Uuid()
            {
                Value = Guid.NewGuid().ToString()
            }
        };
        var signature = fixture.Create<byte[]>();
        var unitOfWork = Substitute.For<Database.IUnitOfWork>();
        unitOfWork.CertificateRepository.GetWalletSlice(Arg.Any<Guid>()).Returns(new WalletSlice()
        {
            Id = Guid.NewGuid(),
            RegistryName = registryName,
            CertificateId = Guid.NewGuid(),
            State = WalletSliceState.Available,
            RandomR = new byte[32],
            WalletEndpointId = Guid.NewGuid(),
            WalletEndpointPosition = 0,
            UpdatedAt = DateTime.UtcNow,
            Quantity = 1
        });
        unitOfWork.WalletRepository.GetPrivateKeyForSlice(Arg.Any<Guid>()).Returns(algorithm.GenerateNewPrivateKey());

        var sc = new ServiceCollection();
        sc.Configure<NetworkOptions>(options =>
        {
            options.Registries.Add(registryName, new RegistryInfo() { Url = "http://localhost" });
        });
        sc.AddTransient(x => Substitute.For<ILogger<SendClaimIntentToChroniclerActivity>>());
        sc.AddTransient(x => Substitute.For<ILogger<AllocateActivity>>());
        sc.AddTransient(x => Substitute.For<IClaimMetrics>());
        sc.AddSingleton(unitOfWork);

        var asyncUnaryCall = new AsyncUnaryCall<ClaimIntentResponse>(
                    Task.FromResult(new ClaimIntentResponse
                    {
                        Signature = ByteString.CopyFrom(signature)
                    }),
                    Task.FromResult(new Metadata()),
                    () => Status.DefaultSuccess,
                    () => new Metadata(),
                    () => { });

        var chroniclerServiceMock = new Mock<ChroniclerService.ChroniclerServiceClient>();
        chroniclerServiceMock.Setup(x => x.RegisterClaimIntentAsync(It.IsAny<ClaimIntentRequest>(), null, null, default))
            .Returns(asyncUnaryCall);

        sc.AddTransient(x => new SendClaimIntentToChroniclerActivity(
            x.GetRequiredService<IOptions<NetworkOptions>>(),
            x.GetRequiredService<ILogger<SendClaimIntentToChroniclerActivity>>(),
            channel => chroniclerServiceMock.Object));

        sc.AddMassTransitTestHarness(cfg =>
            {
                cfg.SetKebabCaseEndpointNameFormatter();
                cfg.AddExecuteActivity<SendClaimIntentToChroniclerActivity, SendClaimIntentToChroniclerArguments>();
                cfg.AddExecuteActivity<AllocateActivity, AllocateArguments>();
            });

        await using var provider = sc.BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        //  build the routing slip

        var chroniclerId = Guid.NewGuid();
        var slipBuilder = new RoutingSlipBuilder(NewId.NextGuid());
        var commitmentInfo = new SecretCommitmentInfo(5);
        var addr = harness.GetExecuteActivityAddress<SendClaimIntentToChroniclerActivity, SendClaimIntentToChroniclerArguments>();
        slipBuilder.AddActivity(nameof(SendClaimIntentToChroniclerActivity), addr, new SendClaimIntentToChroniclerArguments
        {
            Id = chroniclerId,
            CertificateId = certId,
            Quantity = (int)commitmentInfo.Message,
            RandomR = commitmentInfo.BlindingValue.ToArray()
        });
        slipBuilder.AddActivity(nameof(AllocateActivity), harness.GetExecuteActivityAddress<AllocateActivity, AllocateArguments>(), new AllocateArguments
        {
            AllocationId = Guid.NewGuid(),
            ChroniclerRequestId = chroniclerId,
            CertificateId = certId,
            RequestStatusArgs = new RequestStatusArgs
            {
                Owner = "",
                RequestId = Guid.NewGuid(),
                RequestStatusType = RequestStatusType.Claim
            },
            ConsumptionSliceId = Guid.NewGuid(),
            ProductionSliceId = Guid.NewGuid(),
        });

        // Act
        await harness.Bus.Execute(slipBuilder.Build());

        // Assert
        (await harness.Published.Any<RoutingSlipRevised>()).Should().BeTrue("Routing slip should be revised after execution");
    }
}
