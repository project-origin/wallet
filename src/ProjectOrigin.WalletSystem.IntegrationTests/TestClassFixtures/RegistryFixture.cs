using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.PedersenCommitment;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class RegistryFixture : IAsyncLifetime
{
    private const string RegistryImage = "ghcr.io/project-origin/registry-server:0.2.0-rc.17";
    private const string ElectricityVerifierImage = "ghcr.io/project-origin/electricity-server:0.2.0-rc.17";
    private const int GrpcPort = 80;
    private const string Area = "Narnia";
    private const string RegistryName = "TestRegistry";

    private Lazy<IContainer> _registryContainer;
    private IContainer _verifierContainer;

    public string IssuerArea => Area;
    public string Name => RegistryName;
    public IPrivateKey IssuerKey { get; init; }


    public string RegistryUrl => $"http://{_registryContainer.Value.Hostname}:{_registryContainer.Value.GetMappedPublicPort(GrpcPort)}";

    public RegistryFixture()
    {
        IssuerKey = Algorithms.Ed25519.GenerateNewPrivateKey();

        _verifierContainer = new ContainerBuilder()
                .WithImage(ElectricityVerifierImage)
                .WithPortBinding(GrpcPort, true)
                .WithEnvironment($"Issuers__{IssuerArea}", Convert.ToBase64String(Encoding.UTF8.GetBytes(IssuerKey.PublicKey.ExportPkixText())))
                .WithWaitStrategy(
                    Wait.ForUnixContainer()
                        .UntilPortIsAvailable(GrpcPort)
                    )
                .Build();

        _registryContainer = new Lazy<IContainer>(() =>
            {
                var verifierUrl = $"http://{_verifierContainer.IpAddress}:{GrpcPort}";
                return new ContainerBuilder()
                    .WithImage(RegistryImage)
                    .WithPortBinding(GrpcPort, true)
                    .WithEnvironment($"Verifiers__project_origin.electricity.v1", verifierUrl)
                    .WithEnvironment($"RegistryName", RegistryName)
                    .WithEnvironment($"IMMUTABLELOG__TYPE", "log")
                    .WithEnvironment($"VERIFIABLEEVENTSTORE__BATCHSIZEEXPONENT", "0")
                    .WithWaitStrategy(
                        Wait.ForUnixContainer()
                            .UntilPortIsAvailable(GrpcPort)
                        )
                    .Build();
            });
    }

    public async Task InitializeAsync()
    {
        await _verifierContainer.StartAsync()
            .ConfigureAwait(false);

        await _registryContainer.Value.StartAsync()
            .ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (_registryContainer.IsValueCreated)
            await _registryContainer.Value.StopAsync();
        await _verifierContainer.StopAsync();
    }

    public async Task<Electricity.V1.IssuedEvent> IssueCertificate(Electricity.V1.GranularCertificateType type, SecretCommitmentInfo commitment, IPublicKey ownerKey)
    {
        var id = new Common.V1.FederatedStreamId
        {
            Registry = RegistryName,
            StreamId = new Common.V1.Uuid { Value = Guid.NewGuid().ToString() }
        };

        var issuedEvent = new Electricity.V1.IssuedEvent
        {
            CertificateId = id,
            Type = type,
            Period = new Electricity.V1.DateInterval { Start = Timestamp.FromDateTimeOffset(DateTimeOffset.Now), End = Timestamp.FromDateTimeOffset(DateTimeOffset.Now.AddHours(1)) },
            GridArea = Area,
            QuantityCommitment = new Electricity.V1.Commitment
            {
                Content = ByteString.CopyFrom(commitment.Commitment.C),
                RangeProof = ByteString.CopyFrom(commitment.CreateRangeProof(id.StreamId.Value))
            },
            OwnerPublicKey = new Electricity.V1.PublicKey
            {
                Content = ByteString.CopyFrom(ownerKey.Export()),
                Type = Electricity.V1.KeyType.Secp256K1
            }
        };

        var channel = GrpcChannel.ForAddress(RegistryUrl);
        var client = new Registry.V1.RegistryService.RegistryServiceClient(channel);

        var header = new Registry.V1.TransactionHeader
        {
            FederatedStreamId = id,
            PayloadType = Electricity.V1.IssuedEvent.Descriptor.FullName,
            PayloadSha512 = ByteString.CopyFrom(SHA512.HashData(issuedEvent.ToByteArray())),
            Nonce = Guid.NewGuid().ToString(),
        };
        var transactions = new Registry.V1.Transaction
        {
            Header = header,
            HeaderSignature = ByteString.CopyFrom(IssuerKey.Sign(header.ToByteArray())),
            Payload = issuedEvent.ToByteString()
        };

        var request = new Registry.V1.SendTransactionsRequest();
        request.Transactions.Add(transactions);

        await client.SendTransactionsAsync(request);

        var statusRequest = new Registry.V1.GetTransactionStatusRequest
        {
            Id = Convert.ToBase64String(SHA256.HashData(transactions.ToByteArray()))
        };

        var began = DateTime.Now;
        while (true)
        {
            var status = await client.GetTransactionStatusAsync(statusRequest);

            if (status.Status == Registry.V1.TransactionState.Committed)
                break;
            else if (status.Status == Registry.V1.TransactionState.Failed)
                throw new Exception("Failed to issue certificate");
            else
                await Task.Delay(1000);

            if (DateTime.Now - began > TimeSpan.FromMinutes(1))
                throw new Exception("Timed out waiting for transaction to commit");
        }

        return issuedEvent;
    }
}
