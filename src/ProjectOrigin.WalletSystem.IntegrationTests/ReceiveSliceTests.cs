using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System;
using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.V1;
using Xunit;
using Google.Protobuf;
using Xunit.Abstractions;
using AutoFixture;
using MassTransit.Testing;
using ProjectOrigin.WalletSystem.Server.CommandHandlers;
using FluentAssertions;
using System.Linq;
using MassTransit;
using ProjectOrigin.WalletSystem.IntegrationTests.TestExtensions;

namespace ProjectOrigin.WalletSystem.IntegrationTests
{
    public class ReceiveSliceTests : WalletSystemTestsBase, IClassFixture<InMemoryFixture>
    {
        public ReceiveSliceTests(
            GrpcTestFixture<Startup> grpcFixture,
            PostgresDatabaseFixture dbFixture,
            InMemoryFixture inMemoryFixture,
            ITestOutputHelper outputHelper)
            : base(
                  grpcFixture,
                  dbFixture,
                  inMemoryFixture,
                  outputHelper,
                  null)
        {
            grpcFixture.ConfigureTestServices += services =>
            {
                services.AddMassTransitTestHarness();
            };
        }

        [Fact]
        public async Task ReceiveSlice()
        {
            //Arrange
            var certId = Guid.NewGuid();
            var owner = "John";
            var registryName = new Fixture().Create<string>();
            var endpoint = await _dbFixture.CreateReceiveEndpoint(owner);
            var client = new ReceiveSliceService.ReceiveSliceServiceClient(_grpcFixture.Channel);
            var request = new ReceiveRequest()
            {
                CertificateId = new Common.V1.FederatedStreamId()
                {
                    Registry = registryName,
                    StreamId = new Common.V1.Uuid() { Value = certId.ToString() },
                },
                WalletDepositEndpointPublicKey = ByteString.CopyFrom(endpoint.PublicKey.Export()),
                WalletDepositEndpointPosition = 2,
                Quantity = 240,
                RandomR = ByteString.CopyFrom(new byte[] { 0x01, 0x02, 0x03, 0x04 }),
            };

            var harness = _grpcFixture.GetRequiredService<ITestHarness>();

            //Act
            await client.ReceiveSliceAsync(request);

            //Assert
            var publishedMessage = await harness.Published.SelectAsync<VerifySliceCommand>().First();

            publishedMessage.MessageObject.Should().BeOfType<VerifySliceCommand>();
            var command = (VerifySliceCommand)publishedMessage.MessageObject;

            command.WalletEndpointId.Should().Be(endpoint.Id);
            command.WalletEndpointPosition.Should().Be(2);
            command.Registry.Should().Be(registryName);
            command.CertificateId.Should().Be(certId);
            command.Quantity.Should().Be(240);
            command.RandomR.Should().BeEquivalentTo(new byte[] { 0x01, 0x02, 0x03, 0x04 });
        }
    }
}
