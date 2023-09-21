using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.PedersenCommitment;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;

public class RegistryFixture : IAsyncLifetime
{
    private const string RegistryImage = "ghcr.io/project-origin/registry-server:0.2.0";
    private const string ElectricityVerifierImage = "ghcr.io/project-origin/electricity-server:0.2.0";
    private const int GrpcPort = 80;
    private const string Area = "Narnia";
    private const string RegistryName = "TestRegistry";
    private const string RegistryAlias = "registry-container";
    private const string VerifierAlias = "verifier-container";

    private readonly IContainer _registryContainer;
    private readonly IContainer _verifierContainer;
    private readonly INetwork _network;

    public string IssuerArea => Area;
    public string Name => RegistryName;
    public IPrivateKey IssuerKey { get; init; }
    public string RegistryUrl => $"http://{_registryContainer.Hostname}:{_registryContainer.GetMappedPublicPort(GrpcPort)}";

    public RegistryFixture()
    {
        IssuerKey = Algorithms.Ed25519.GenerateNewPrivateKey();

        _network = new NetworkBuilder()
            .WithName(Guid.NewGuid().ToString())
            .Build();

        _verifierContainer = new ContainerBuilder()
                .WithImage(ElectricityVerifierImage)
                .WithNetwork(_network)
                .WithNetworkAliases(VerifierAlias)
                .WithPortBinding(GrpcPort, true)
                .WithEnvironment($"Issuers__{IssuerArea}", Convert.ToBase64String(Encoding.UTF8.GetBytes(IssuerKey.PublicKey.ExportPkixText())))
                .WithEnvironment($"Registries__{RegistryName}__Address", $"http://{RegistryAlias}:{GrpcPort}")
                .WithWaitStrategy(
                    Wait.ForUnixContainer()
                        .UntilPortIsAvailable(GrpcPort)
                    )
                .Build();

        _registryContainer = new ContainerBuilder()
                    .WithImage(RegistryImage)
                    .WithNetwork(_network)
                    .WithNetworkAliases(RegistryAlias)
                    .WithPortBinding(GrpcPort, true)
                    .WithEnvironment($"Verifiers__project_origin.electricity.v1", $"http://{VerifierAlias}:{GrpcPort}")
                    .WithEnvironment($"RegistryName", RegistryName)
                    .WithEnvironment($"IMMUTABLELOG__TYPE", "log")
                    .WithEnvironment($"VERIFIABLEEVENTSTORE__BATCHSIZEEXPONENT", "0")
                    .WithWaitStrategy(
                        Wait.ForUnixContainer()
                            .UntilPortIsAvailable(GrpcPort)
                        )
                    .Build();
    }

    public async Task InitializeAsync()
    {
        await _network.CreateAsync()
            .ConfigureAwait(false);

        await _verifierContainer.StartAsync()
            .ConfigureAwait(false);

        await _registryContainer.StartAsync()
            .ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _registryContainer.StopAsync().ConfigureAwait(false);
        await _verifierContainer.StopAsync().ConfigureAwait(false);

        await _registryContainer.DisposeAsync().ConfigureAwait(false);
        await _verifierContainer.DisposeAsync().ConfigureAwait(false);

        await _network.DeleteAsync().ConfigureAwait(false);
        await _network.DisposeAsync().ConfigureAwait(false);
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
            Period = new Electricity.V1.DateInterval
            {
                Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2023, 1, 10, 12, 0, 0, TimeSpan.Zero)),
                End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2023, 1, 10, 13, 0, 0, TimeSpan.Zero))
            },
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
