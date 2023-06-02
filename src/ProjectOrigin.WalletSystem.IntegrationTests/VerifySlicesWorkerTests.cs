using AutoFixture;
using FluentAssertions;
using Npgsql;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using ProjectOrigin.WalletSystem.Server.Models;
using System;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class VerifySlicesWorkerTests : WalletSystemTestsBase
{
    public VerifySlicesWorkerTests(GrpcTestFixture<Startup> grpcFixture, PostgresDatabaseFixture dbFixture, ITestOutputHelper outputHelper)
        : base(grpcFixture, dbFixture, outputHelper)
    {
    }

    [Fact]
    public async void WhenReceivedSliceWithNoCertificate_ExpectIsConvertedToSliceAndCertificateIsCreated()
    {
        var certId = Guid.NewGuid();
        var owner = "SomeOwner";
        var registryName = new Fixture().Create<string>();
        var section = await CreateWalletSection(owner);
        var registry = await CreateRegistry(registryName);
        var receivedSlice = await CreateReceivedSlice(section, registryName, certId, 42);

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

    [Fact]
    public async void WhenReceivedSliceWithCertificate_ExpectIsConvertedToSliceWithCertificate()
    {
        var certId = Guid.NewGuid();
        var owner = "SomeOtherOwner";
        var registryName = new Fixture().Create<string>();
        var section = await CreateWalletSection(owner);
        var registry = await CreateRegistry(registryName);
        var certificate = await CreateCertificate(certId, registry.Id);
        var receivedSlice = await CreateReceivedSlice(section, registryName, certId, 42);

        await using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var slice = await connection.RepeatedlyQueryFirstOrDefaultUntil<Slice>("SELECT * FROM Slices WHERE certificateId = @id", new { id = certId });
            slice.Should().NotBeNull();
            slice.CertificateId.Should().Be(certificate.Id);
        }
    }
}
