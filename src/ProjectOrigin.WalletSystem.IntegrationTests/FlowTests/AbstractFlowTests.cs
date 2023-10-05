using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System.Collections.Generic;
using Xunit.Abstractions;
using System.Threading.Tasks;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.HierarchicalDeterministicKeys;
using Google.Protobuf;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public abstract class AbstractFlowTests : WalletSystemTestsBase, IClassFixture<RegistryFixture>, IClassFixture<InMemoryFixture>
{
    private readonly RegistryFixture _registryFixture;

    public AbstractFlowTests(
        GrpcTestFixture<Startup> grpcFixture,
        PostgresDatabaseFixture dbFixture,
        IMessageBrokerFixture messageBrokerFixture,
        ITestOutputHelper outputHelper,
        RegistryFixture registryFixture) : base(grpcFixture, dbFixture, messageBrokerFixture, outputHelper, registryFixture)
    {
        _registryFixture = registryFixture;
    }

    protected async Task<Common.V1.FederatedStreamId> IssueCertificateToEndpoint(
        V1.CreateWalletDepositEndpointResponse endpoint,
        Electricity.V1.GranularCertificateType type,
        SecretCommitmentInfo issuedCommitment,
        int position,
        Dictionary<string, string>? att = null)
    {
        var publicKey = Algorithms.Secp256k1.ImportHDPublicKey(endpoint.WalletDepositEndpoint.PublicKey.Span);

        var issuedEvent = await _registryFixture.IssueCertificate(
            type,
            issuedCommitment,
            publicKey.Derive(position).GetPublicKey(),
            att);

        var receiveClient = new V1.ReceiveSliceService.ReceiveSliceServiceClient(_grpcFixture.Channel);
        await receiveClient.ReceiveSliceAsync(new V1.ReceiveRequest()
        {
            WalletDepositEndpointPublicKey = endpoint.WalletDepositEndpoint.PublicKey,
            WalletDepositEndpointPosition = (uint)position,
            CertificateId = issuedEvent.CertificateId,
            Quantity = issuedCommitment.Message,
            RandomR = ByteString.CopyFrom(issuedCommitment.BlindingValue),
        });
        return issuedEvent.CertificateId;
    }
}
