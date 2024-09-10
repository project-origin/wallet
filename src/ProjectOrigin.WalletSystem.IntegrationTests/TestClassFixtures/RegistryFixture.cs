using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.PedersenCommitment;
using Testcontainers.RabbitMq;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;

public class RegistryFixture : IAsyncLifetime
{
    private const string RegistryImage = "ghcr.io/project-origin/registry-server:1.3.1";
    private const string ElectricityVerifierImage = "ghcr.io/project-origin/electricity-server:1.1.0";
    private const int RabbitMqHttpPort = 15672;
    private const int GrpcPort = 5000;
    private const string Area = "Narnia";
    private const string RegistryName = "TestRegistry";
    private const string RegistryAlias = "registry-container";
    private const string VerifierAlias = "verifier-container";
    private const string RabbitMqAlias = "rabbitmq-container";

    private readonly INetwork _network;
    private readonly IContainer _registryContainer;
    private readonly IContainer _verifierContainer;
    private readonly IContainer _rabbitMqContainer;
    private readonly IFutureDockerImage _rabbitMqImage;

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

        _rabbitMqImage = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(CommonDirectoryPath.GetProjectDirectory(), string.Empty)
            .WithDockerfile("rabbitmq.dockerfile")
            .Build();

        _rabbitMqContainer = new RabbitMqBuilder()
            .WithImage(_rabbitMqImage)
            .WithNetwork(_network)
            .WithNetworkAliases(RabbitMqAlias)
            .WithPortBinding(RabbitMqHttpPort, true)
            .Build();

        _verifierContainer = new ContainerBuilder()
                .WithImage(ElectricityVerifierImage)
                .WithNetwork(_network)
                .WithNetworkAliases(VerifierAlias)
                .WithPortBinding(GrpcPort, true)
                .WithCommand("--serve")
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
                    .WithCommand("--migrate", "--serve")
                    .WithEnvironment($"RegistryName", RegistryName)
                    .WithEnvironment($"Otlp__Enabled", "false")
                    .WithEnvironment($"Verifiers__project_origin.electricity.v1", $"http://{VerifierAlias}:{GrpcPort}")
                    .WithEnvironment($"ImmutableLog__Type", "log")
                    .WithEnvironment($"BlockFinalizer__Interval", "00:00:05")
                    .WithEnvironment($"Persistance__Type", "in_memory")
                    .WithEnvironment($"Cache__Type", "InMemory")
                    .WithEnvironment("RabbitMq__Hostname", RabbitMqAlias)
                    .WithEnvironment("RabbitMq__AmqpPort", RabbitMqBuilder.RabbitMqPort.ToString())
                    .WithEnvironment("RabbitMq__HttpApiPort", RabbitMqHttpPort.ToString())
                    .WithEnvironment("RabbitMq__Username", RabbitMqBuilder.DefaultUsername)
                    .WithEnvironment("RabbitMq__Password", RabbitMqBuilder.DefaultPassword)
                    .WithEnvironment("TransactionProcessor__ServerNumber", "0")
                    .WithEnvironment("TransactionProcessor__Servers", "1")
                    .WithEnvironment("TransactionProcessor__Threads", "5")
                    .WithEnvironment("TransactionProcessor__Weight", "10")
                    .WithWaitStrategy(
                        Wait.ForUnixContainer()
                            .UntilPortIsAvailable(GrpcPort)
                        )
                    .Build();
    }

    public async Task InitializeAsync()
    {
        await _rabbitMqImage.CreateAsync()
            .ConfigureAwait(false);

        await _network.CreateAsync()
            .ConfigureAwait(false);

        await _rabbitMqContainer.StartAsync()
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

    public async Task<Electricity.V1.IssuedEvent> IssueCertificate(
        Electricity.V1.GranularCertificateType type,
        SecretCommitmentInfo commitment,
        IPublicKey ownerKey,
        List<(string Key, string Value, byte[]? Salt)>? attributes = null)
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

        if (attributes != null)
            issuedEvent.Attributes.Add(attributes.Select(attribute =>
            {
                if (attribute.Salt is null)
                    return new Electricity.V1.Attribute { Key = attribute.Key, Value = attribute.Value, Type = Electricity.V1.AttributeType.Cleartext };
                else
                {
                    var str = attribute.Key + attribute.Value + id.StreamId.Value.ToString() + Convert.ToHexString(attribute.Salt);
                    var hashedValue = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(str)));
                    return new Electricity.V1.Attribute { Key = attribute.Key, Value = hashedValue, Type = Electricity.V1.AttributeType.Hashed };
                }
            }));

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

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            var status = await client.GetTransactionStatusAsync(statusRequest);

            if (status.Status == Registry.V1.TransactionState.Committed)
                break;
            else if (status.Status == Registry.V1.TransactionState.Failed)
                throw new Exception("Failed to issue certificate");

            if (stopwatch.Elapsed > TimeSpan.FromSeconds(15))
            {
                var registryLog = await _registryContainer.GetLogsAsync();
                var verifierLog = await _verifierContainer.GetLogsAsync();

                throw new Exception($"Timed out waiting for transaction to commit {status.Status},\n\nRegistry Log:\n{registryLog}\n\nVerifier Log:\n{verifierLog}");
            }

            await Task.Delay(1000);
        }

        return issuedEvent;
    }
}
