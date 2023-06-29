using AutoFixture;
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

    public VerifySlicesWorkerTests(GrpcTestFixture<Startup> grpcFixture, RegistryFixture registryFixture, PostgresDatabaseFixture dbFixture, ITestOutputHelper outputHelper)
        : base(grpcFixture, dbFixture, outputHelper)
    {
        _registryFixture = registryFixture;
    }

    [Fact]
    public async void WhenReceivedSliceWithNoCertificate_ExpectIsConvertedToSliceAndCertificateIsCreated()
    {
        var owner = new Fixture().Create<string>();
        var section = await CreateWalletSection(owner);

        var commitment = new SecretCommitmentInfo(150);

        var b = await _registryFixture.IssueCertificate(Electricity.V1.GranularCertificateType.Production, commitment, section.PublicKey.GetPublicKey());
        var certId = Guid.Parse(b.CertificateId.StreamId.Value);

        var receivedSlice = await CreateReceivedSlice(section, b.CertificateId.Registry, certId, commitment.Message, commitment.BlindingValue.ToArray());

        await using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var cert = await connection.RepeatedlyQueryFirstOrDefaultUntil<Certificate>("SELECT * FROM Certificates WHERE id = @id", new { id = certId });
            var slice = await connection.RepeatedlyQueryFirstOrDefaultUntil<Slice>("SELECT * FROM Slices WHERE certificateId = @id", new { id = certId });
            cert.Should().NotBeNull();
            cert.Id.Should().Be(receivedSlice.CertificateId);
            slice.Should().NotBeNull();
            slice.CertificateId.Should().Be(receivedSlice.CertificateId);
        }
    }

    //[Fact]
    public async void WhenReceivedSliceWithCertificate_ExpectIsConvertedToSliceWithCertificate()
    {
        var certId = Guid.NewGuid();
        var owner = new Fixture().Create<string>();
        var registryName = _registryFixture.Name;
        var section = await CreateWalletSection(owner);
        var registry = await CreateRegistry(registryName);
        var certificate = await CreateCertificate(certId, registry.Id);
        var receivedSlice = await CreateReceivedSlice(section, registryName, certId, 42, null);

        await using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var slice = await connection.RepeatedlyQueryFirstOrDefaultUntil<Slice>("SELECT * FROM Slices WHERE certificateId = @id", new { id = certId });
            slice.Should().NotBeNull();
            slice.CertificateId.Should().Be(certificate.Id);
        }
    }

    //[Fact]
    public async void WhenReceivedSliceHasUnknownRegistry_ExpectReceivedSliceDeleted()
    {
        var certId = Guid.NewGuid();
        var owner = new Fixture().Create<string>();
        var registryName = _registryFixture.Name;
        var section = await CreateWalletSection(owner);
        var receivedSlice = await CreateReceivedSlice(section, registryName, certId, 42, null);

        await using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var rSlice = await connection.RepeatedlyQueryUntilNull<ReceivedSlice>("SELECT * FROM ReceivedSlices WHERE certificateId = @id", new { id = certId });
            rSlice.Should().BeNull();
        }
    }

}
