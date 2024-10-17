using System.Threading.Tasks;
using Xunit;
using Moq;
using ProjectOrigin.Vault.Activities;
using ProjectOrigin.Chronicler.V1;
using Microsoft.Extensions.Logging;
using ProjectOrigin.Vault.Options;
using ProjectOrigin.Common.V1;
using Google.Protobuf;
using MassTransit;
using FluentAssertions;
using Grpc.Core;
using System.Collections.Generic;
using ProjectOrigin.Vault.Activities.Exceptions;
using AutoFixture;
using System.Linq;
using System;

namespace ProjectOrigin.Vault.Tests.ActivityTests
{
    public class SendClaimIntentToChroniclerActivityTestsTest
    {
        [Fact]
        public async Task ExecuteAsync_ShouldCallChroniclerService()
        {
            // Arrange
            var fixture = new Fixture();
            var registryName = "MyRegistry";
            var signature = fixture.Create<byte[]>();

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

            var arguments = new SendClaimIntentToChroniclerArgument
            {
                Id = System.Guid.NewGuid(),
                ClaimIntentRequest = new ClaimIntentRequest()
                {
                    CertificateId = new FederatedStreamId()
                    {
                        Registry = registryName,
                        StreamId = new Uuid
                        {
                            Value = System.Guid.NewGuid().ToString()
                        }
                    },
                    Quantity = 1,
                    RandomR = ByteString.CopyFrom(fixture.Create<byte[]>()),
                }
            };

            var returnValue = Mock.Of<ExecutionResult>();
            var context = new Mock<ExecuteContext<SendClaimIntentToChroniclerArgument>>(MockBehavior.Strict);
            context.Setup(x => x.TrackingNumber).Returns(System.Guid.NewGuid());
            context.Setup(x => x.ActivityName).Returns(nameof(SendClaimIntentToChroniclerActivity));
            context.Setup(x => x.Arguments).Returns(arguments);
            context.Setup(x => x.CompletedWithVariables(It.Is<Dictionary<string, object>>(x => (x[arguments.Id.ToString()] as byte[])!.SequenceEqual(signature)))).Returns(returnValue);

            var options = new NetworkOptions()
            {
                Registries = new Dictionary<string, RegistryInfo>()
                {
                    {
                        registryName, new RegistryInfo(){ Url = "http://localhost" }
                    }
                }
            };

            var activity = new SendClaimIntentToChroniclerActivity(
                Microsoft.Extensions.Options.Options.Create(options),
                Mock.Of<ILogger<SendClaimIntentToChroniclerActivity>>(),
                x => chroniclerServiceMock.Object);

            // Act
            var result = await activity.Execute(context.Object);

            // Assert
            result.Should().Be(returnValue);

        }

        [Fact]
        public async Task ExecuteAsync_ShouldFail_UnknownRegistry()
        {
            // Arrange
            var registryName = "MyRegistry";

            var arguments = new SendClaimIntentToChroniclerArgument
            {
                Id = System.Guid.NewGuid(),
                ClaimIntentRequest = new ClaimIntentRequest()
                {
                    CertificateId = new FederatedStreamId()
                    {
                        Registry = registryName,
                        StreamId = new Uuid
                        {
                            Value = System.Guid.NewGuid().ToString()
                        }
                    },
                    Quantity = 1,
                    RandomR = ByteString.CopyFrom(new byte[] { 1, 2, 3 }),
                }
            };

            var context = new Mock<ExecuteContext<SendClaimIntentToChroniclerArgument>>(MockBehavior.Strict);
            context.Setup(x => x.TrackingNumber).Returns(System.Guid.NewGuid());
            context.Setup(x => x.ActivityName).Returns(nameof(SendClaimIntentToChroniclerActivity));
            context.Setup(x => x.Arguments).Returns(arguments);

            var options = new NetworkOptions();

            var activity = new SendClaimIntentToChroniclerActivity(
                Microsoft.Extensions.Options.Options.Create(options),
                Mock.Of<ILogger<SendClaimIntentToChroniclerActivity>>(),
                x => Mock.Of<ChroniclerService.ChroniclerServiceClient>());

            // Act
            var ex = await Assert.ThrowsAsync<ChroniclerException>(async () => await activity.Execute(context.Object));

            // Assert
            ex.Message.Should().Be("Error registering claim intent with Chronicler");
            ex.InnerException.Should().BeOfType<ArgumentException>().Which.Message.Should().Be($"Registry with name {registryName} not found in configuration.");
        }
    }
}
