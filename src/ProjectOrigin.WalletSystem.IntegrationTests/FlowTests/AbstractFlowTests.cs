using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System.Collections.Generic;
using Xunit.Abstractions;
using System.Threading.Tasks;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.HierarchicalDeterministicKeys;
using Google.Protobuf;
using Xunit;
using System;
using System.Linq;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public abstract class AbstractFlowTests : WalletSystemTestsBase, IClassFixture<RegistryFixture>, IClassFixture<InMemoryFixture>, IClassFixture<JwtTokenIssuerFixture>
{
    private readonly RegistryFixture _registryFixture;

    public AbstractFlowTests(
        GrpcTestFixture<Startup> grpcFixture,
        PostgresDatabaseFixture dbFixture,
        IMessageBrokerFixture messageBrokerFixture,
        JwtTokenIssuerFixture jwtTokenIssuerFixture,
        ITestOutputHelper outputHelper,
        RegistryFixture registryFixture) : base(grpcFixture, dbFixture, messageBrokerFixture, jwtTokenIssuerFixture, outputHelper, registryFixture)
    {
        _registryFixture = registryFixture;
    }

    protected async Task<Common.V1.FederatedStreamId> IssueCertificateToEndpoint(
        V1.WalletDepositEndpoint endpoint,
        Electricity.V1.GranularCertificateType type,
        SecretCommitmentInfo issuedCommitment,
        int position,
        List<(string Key, string Value, byte[]? Salt)>? attributes = null)
    {
        var publicKey = Algorithms.Secp256k1.ImportHDPublicKey(endpoint.PublicKey.Span);

        var issuedEvent = await _registryFixture.IssueCertificate(
            type,
            issuedCommitment,
            publicKey.Derive(position).GetPublicKey(),
            attributes);

        var receiveClient = new V1.ReceiveSliceService.ReceiveSliceServiceClient(_grpcFixture.Channel);

        var request = new V1.ReceiveRequest()
        {
            WalletDepositEndpointPublicKey = endpoint.PublicKey,
            WalletDepositEndpointPosition = (uint)position,
            CertificateId = issuedEvent.CertificateId,
            Quantity = issuedCommitment.Message,
            RandomR = ByteString.CopyFrom(issuedCommitment.BlindingValue),
        };

        if (attributes is not null)
            request.HashedAttributes.Add(attributes.Where(attribute => attribute.Salt is not null).Select(attribute => new V1.ReceiveRequest.Types.HashedAttribute
            {
                Key = attribute.Key,
                Value = attribute.Value,
                Salt = ByteString.CopyFrom(attribute.Salt)
            }));

        await receiveClient.ReceiveSliceAsync(request);
        return issuedEvent.CertificateId;
    }

    protected static async Task<T> Timeout<T>(Func<Task<T>> func, TimeSpan timeout)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                return await func();
            }
            catch (Exception)
            {
                await Task.Delay(1000);
            }
        }
        throw new TimeoutException();
    }
}
