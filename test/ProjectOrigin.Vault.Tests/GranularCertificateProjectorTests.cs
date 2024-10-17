
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using AutoFixture;
using FluentAssertions;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.Vault.Projections;
using Xunit;

namespace ProjectOrigin.Vault.Tests;

public class GranularCertificateProjectorTests
{
    private Fixture _fixture = new Fixture();

    [Fact]
    public void Project_WhenEventStreamIsEmpty_ThrowsProjectionException()
    {
        // Arrange
        var projector = new GranularCertificateProjector();
        var emptyEventStream = new List<Registry.V1.Transaction>();

        // Act & Assert
        Assert.Throws<ProjectionException>(() => projector.Project(emptyEventStream));
    }

    [Fact]
    public void Project_WhenEventStreamHasIssueTransaction_ReturnsCertificate()
    {
        // Arrange
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var issuerKey = Algorithms.Ed25519.GenerateNewPrivateKey();

        var id = new Common.V1.FederatedStreamId
        {
            Registry = _fixture.Create<string>(),
            StreamId = new Common.V1.Uuid { Value = Guid.NewGuid().ToString() }
        };

        var issuedSize = new SecretCommitmentInfo(150);
        var issued = Issue(id, issuedSize, ownerKey.Derive(1).PublicKey);

        var projector = new GranularCertificateProjector();
        var eventStream = new List<Registry.V1.Transaction>
        {
            CreateAndSignTransaction(id, issued, issuerKey)
        };

        // Act
        var certificate = projector.Project(eventStream);

        // Assert
        certificate.Should().NotBeNull();
    }

    [Fact]
    public void Project_WhenIssuedAndSliced_ReturnsCertificate()
    {
        // Arrange
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var issuerKey = Algorithms.Ed25519.GenerateNewPrivateKey();

        var id = new Common.V1.FederatedStreamId
        {
            Registry = _fixture.Create<string>(),
            StreamId = new Common.V1.Uuid { Value = Guid.NewGuid().ToString() }
        };

        var issuedSize = new SecretCommitmentInfo(150);
        var issued = Issue(id, issuedSize, ownerKey.Derive(1).PublicKey);

        var sliceA = new SecretCommitmentInfo(100);
        var sliceB = new SecretCommitmentInfo(100);
        var sliced = Slice(id, issuedSize, sliceA, sliceB, ownerKey.Derive(1).PublicKey);

        var projector = new GranularCertificateProjector();
        var eventStream = new List<Registry.V1.Transaction>
        {
            CreateAndSignTransaction(id, issued, issuerKey),
            CreateAndSignTransaction(id, sliced, ownerKey.Derive(1))
        };

        // Act
        var certificate = projector.Project(eventStream);

        // Assert
        certificate.Should().NotBeNull();
    }

    [Fact]
    public void Project_WhenIssuedSlicedAndWithdrawn_ReturnsCertificate()
    {
        // Arrange
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var issuerKey = Algorithms.Ed25519.GenerateNewPrivateKey();

        var id = new Common.V1.FederatedStreamId
        {
            Registry = _fixture.Create<string>(),
            StreamId = new Common.V1.Uuid { Value = Guid.NewGuid().ToString() }
        };

        var issuedSize = new SecretCommitmentInfo(150);
        var issued = Issue(id, issuedSize, ownerKey.Derive(1).PublicKey);

        var sliceA = new SecretCommitmentInfo(100);
        var sliceB = new SecretCommitmentInfo(100);
        var sliced = Slice(id, issuedSize, sliceA, sliceB, ownerKey.Derive(1).PublicKey);

        var withdraw = Withdraw();

        var projector = new GranularCertificateProjector();
        var eventStream = new List<Registry.V1.Transaction>
        {
            CreateAndSignTransaction(id, issued, issuerKey),
            CreateAndSignTransaction(id, sliced, ownerKey.Derive(1)),
            CreateAndSignTransaction(id, withdraw, ownerKey.Derive(2))
        };

        // Act
        var certificate = projector.Project(eventStream);

        // Assert
        certificate.Should().NotBeNull();
        certificate.Withdrawn.Should().BeTrue();
    }

    private static Electricity.V1.WithdrawnEvent Withdraw()
    {
        return new Electricity.V1.WithdrawnEvent
        { };
    }

    private static Electricity.V1.IssuedEvent Issue(Common.V1.FederatedStreamId id, SecretCommitmentInfo commitment, IPublicKey publicKey)
    {
        return new Electricity.V1.IssuedEvent
        {
            CertificateId = id,
            Type = Electricity.V1.GranularCertificateType.Consumption,
            Period = new Electricity.V1.DateInterval
            {
                Start = Timestamp.FromDateTimeOffset(new DateTimeOffset(2023, 1, 10, 12, 0, 0, TimeSpan.Zero)),
                End = Timestamp.FromDateTimeOffset(new DateTimeOffset(2023, 1, 10, 13, 0, 0, TimeSpan.Zero))
            },
            GridArea = "Narnia",
            QuantityCommitment = new Electricity.V1.Commitment
            {
                Content = ByteString.CopyFrom(commitment.Commitment.C),
                RangeProof = ByteString.CopyFrom(commitment.CreateRangeProof(id.StreamId.Value))
            },
            OwnerPublicKey = new Electricity.V1.PublicKey
            {
                Content = ByteString.CopyFrom(publicKey.Export()),
                Type = Electricity.V1.KeyType.Secp256K1
            }
        };
    }

    private static Electricity.V1.SlicedEvent Slice(Common.V1.FederatedStreamId id, SecretCommitmentInfo source, SecretCommitmentInfo a, SecretCommitmentInfo b, IPublicKey publicKey)
    {
        var equalityProof = SecretCommitmentInfo.CreateEqualityProof(source, a + b, id.StreamId.Value);

        return new Electricity.V1.SlicedEvent
        {
            CertificateId = id,
            NewSlices = {
                new Electricity.V1.SlicedEvent.Types.Slice{
                    Quantity  = new Electricity.V1.Commitment
                    {
                        Content = ByteString.CopyFrom(a.Commitment.C),
                        RangeProof = ByteString.CopyFrom(a.CreateRangeProof(id.StreamId.Value))
                    },
                    NewOwner = new Electricity.V1.PublicKey
                    {
                        Content = ByteString.CopyFrom(publicKey.Export()),
                        Type = Electricity.V1.KeyType.Secp256K1
                    }
                },
                new Electricity.V1.SlicedEvent.Types.Slice{
                    Quantity  = new Electricity.V1.Commitment
                    {
                        Content = ByteString.CopyFrom(b.Commitment.C),
                        RangeProof = ByteString.CopyFrom(b.CreateRangeProof(id.StreamId.Value))
                    },
                    NewOwner = new Electricity.V1.PublicKey
                    {
                        Content = ByteString.CopyFrom(publicKey.Export()),
                        Type = Electricity.V1.KeyType.Secp256K1
                    }
                }
            },
            SourceSliceHash = ByteString.CopyFrom(SHA256.HashData(source.Commitment.C)),
            SumProof = ByteString.CopyFrom(equalityProof),
        };
    }

    private static Registry.V1.Transaction CreateAndSignTransaction(Common.V1.FederatedStreamId federatedStreamId, IMessage payload, IPrivateKey privateKey)
    {
        var header = new Registry.V1.TransactionHeader
        {
            FederatedStreamId = federatedStreamId,
            PayloadType = payload.Descriptor.FullName,
            PayloadSha512 = ByteString.CopyFrom(SHA512.HashData(payload.ToByteArray())),
            Nonce = Guid.NewGuid().ToString(),
        };
        return new Registry.V1.Transaction
        {
            Header = header,
            HeaderSignature = ByteString.CopyFrom(privateKey.Sign(header.ToByteArray())),
            Payload = payload.ToByteString()
        };
    }
}
