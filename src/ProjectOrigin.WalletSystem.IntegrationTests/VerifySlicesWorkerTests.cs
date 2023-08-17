using AutoFixture;
using Dapper;
using FluentAssertions;
using Npgsql;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using ProjectOrigin.WalletSystem.Server.Models;
using System;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class VerifySlicesWorkerTests : WalletSystemTestsBase, IClassFixture<RegistryFixture>
{
    private RegistryFixture _registryFixture;
    private Fixture _fixture;

    public VerifySlicesWorkerTests(GrpcTestFixture<Startup> grpcFixture, RegistryFixture registryFixture, PostgresDatabaseFixture dbFixture, ITestOutputHelper outputHelper)
        : base(grpcFixture, dbFixture, outputHelper, registryFixture)
    {
        _registryFixture = registryFixture;
        _fixture = new Fixture();
    }

    [Fact]
    public async void WhenReceivedSliceIsValid_ExpectIsConvertedToSliceAndCertificateIsCreated()
    {
        var owner = _fixture.Create<string>();
        var depositEndpoint = await CreateWalletDepositEndpoint(owner);
        var commitment = new SecretCommitmentInfo(150);
        var position = 1;
        var publicKey = depositEndpoint.PublicKey.Derive(position).GetPublicKey();

        var issuedEvent = await _registryFixture.IssueCertificate(Electricity.V1.GranularCertificateType.Production, commitment, publicKey);
        var certId = Guid.Parse(issuedEvent.CertificateId.StreamId.Value);

        var receivedSlice = await CreateReceivedSlice(depositEndpoint, position, issuedEvent.CertificateId.Registry, certId, commitment.Message, commitment.BlindingValue.ToArray());

        await using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var cert = await connection.RepeatedlyQueryFirstOrDefaultUntil<Certificate>("SELECT * FROM Certificates WHERE id = @id", new { id = certId });
            var slice = await connection.RepeatedlyQueryFirstOrDefaultUntil<Slice>("SELECT * FROM Slices WHERE certificateId = @id", new { id = certId });
            cert.Should().NotBeNull();
            cert.Id.Should().Be(receivedSlice.CertificateId);
            slice.Should().NotBeNull();
            slice.CertificateId.Should().Be(receivedSlice.CertificateId);
            slice.Quantity.Should().Be(commitment.Message);
        }
    }

    [Fact]
    public async void WhenReceivedSliceIsUnknownInRegistry_ExpectReceivedSliceDeleted()
    {
        var owner = _fixture.Create<string>();
        var depositEndpoint = await CreateWalletDepositEndpoint(owner);
        var commitment = new SecretCommitmentInfo(150);
        var position = 1;

        var certId = Guid.NewGuid();
        var registryName = _registryFixture.Name;

        await CreateReceivedSlice(depositEndpoint, position, registryName, certId, commitment.Message, commitment.BlindingValue.ToArray());

        await using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var rSlice = await connection.RepeatedlyQueryUntilNull<ReceivedSlice>("SELECT * FROM ReceivedSlices WHERE certificateId = @id", new { id = certId });
            rSlice.Should().BeNull();

            var cert = await connection.QueryAsync<Certificate>("SELECT * FROM Certificates WHERE id = @id", new { id = certId });
            cert.Should().BeEmpty();
        }
    }

    [Fact]
    public async void WhenReceivedSliceIsUnknownRegistry_ExpectReceivedSliceDeleted()
    {
        var owner = _fixture.Create<string>();
        var depositEndpoint = await CreateWalletDepositEndpoint(owner);
        var commitment = new SecretCommitmentInfo(150);
        var position = 1;

        var certId = Guid.NewGuid();
        var registryName = _fixture.Create<string>();

        await CreateReceivedSlice(depositEndpoint, position, registryName, certId, commitment.Message, commitment.BlindingValue.ToArray());

        await using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var rSlice = await connection.RepeatedlyQueryUntilNull<ReceivedSlice>("SELECT * FROM ReceivedSlices WHERE certificateId = @id", new { id = certId });
            rSlice.Should().BeNull();

            var cert = await connection.QueryAsync<Certificate>("SELECT * FROM Certificates WHERE id = @id", new { id = certId });
            cert.Should().BeEmpty();
        }
    }

    [Fact]
    public async void WhenPositionDoesNotMatchKey_ExpectReceivedSliceDeleted()
    {
        var owner = _fixture.Create<string>();
        var depositEndpoint = await CreateWalletDepositEndpoint(owner);
        var commitment = new SecretCommitmentInfo(150);
        var position = 1;

        var publicKey = depositEndpoint.PublicKey.Derive(position + 1).GetPublicKey();

        var issuedEvent = await _registryFixture.IssueCertificate(Electricity.V1.GranularCertificateType.Production, commitment, publicKey);
        var certId = Guid.Parse(issuedEvent.CertificateId.StreamId.Value);

        await CreateReceivedSlice(depositEndpoint, position, issuedEvent.CertificateId.Registry, certId, commitment.Message, commitment.BlindingValue.ToArray());

        await using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var rSlice = await connection.RepeatedlyQueryUntilNull<ReceivedSlice>("SELECT * FROM ReceivedSlices WHERE certificateId = @id", new { id = certId });
            rSlice.Should().BeNull();

            var cert = await connection.QueryAsync<Certificate>("SELECT * FROM Certificates WHERE id = @id", new { id = certId });
            cert.Should().BeEmpty();
        }
    }
}
