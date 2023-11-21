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
            TestServerFixture<Startup> serverFixture,
            PostgresDatabaseFixture dbFixture,
            InMemoryFixture inMemoryFixture,
        JwtTokenIssuerFixture jwtTokenIssuerFixture,
            ITestOutputHelper outputHelper)
            : base(
                  serverFixture,
                  dbFixture,
                  inMemoryFixture,
                  jwtTokenIssuerFixture,
                  outputHelper,
                  null)
        {
            serverFixture.ConfigureTestServices += services =>
            {
                services.AddMassTransitTestHarness();
            };
        }

        [Fact]
        public async Task ReceiveSlice()
        {
            //Arrange
            var certId = Guid.NewGuid();
            var owner = _fixture.Create<string>();
            var registryName = _fixture.Create<string>();
            var endpoint = await _dbFixture.CreateWalletEndpoint(owner);
            var client = new ReceiveSliceService.ReceiveSliceServiceClient(_serverFixture.Channel);
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
            request.HashedAttributes.Add(new ReceiveRequest.Types.HashedAttribute()
            {
                Key = "AssetId",
                Value = "1234",
                Salt = ByteString.CopyFrom(new byte[] { 0x05, 0x06, 0x07, 0x08 }),
            });

            var harness = _serverFixture.GetRequiredService<ITestHarness>();

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
            command.HashedAttributes.Should().HaveCount(1);
            command.HashedAttributes.First().Key.Should().Be("AssetId");
            command.HashedAttributes.First().Value.Should().Be("1234");
            command.HashedAttributes.First().Salt.Should().BeEquivalentTo(new byte[] { 0x05, 0x06, 0x07, 0x08 });
        }
    }
}
