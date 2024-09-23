using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server;
using System;
using FluentAssertions;
using Npgsql;
using ProjectOrigin.WalletSystem.Server.Models;
using Xunit.Abstractions;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class WithdrawCertificatesTests : WalletSystemTestsBase, IClassFixture<InMemoryFixture>
{
    public WithdrawCertificatesTests(
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
    }

    [Fact]
    public async void Test1()
    {
        _serverFixture.Start();

        using (var connection = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var cursor = await connection.RepeatedlyQueryFirstOrDefaultUntil<WithdrawnCursor>("SELECT * FROM withdrawn_cursors", timeLimit: TimeSpan.FromSeconds(45));

            cursor.Should().NotBeNull();
            cursor.StampName.Should().Be("Narnia");
        }
    }
}
