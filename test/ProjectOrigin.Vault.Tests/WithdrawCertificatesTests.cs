using System;
using System.Threading.Tasks;
using FluentAssertions;
using Npgsql;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using Xunit;
using Xunit.Abstractions;

namespace ProjectOrigin.Vault.Tests;

public class WithdrawCertificatesTests : WalletSystemTestsBase, IClassFixture<InMemoryFixture>, IClassFixture<StampAndRegistryFixture>
{
    public WithdrawCertificatesTests(
        TestServerFixture<Startup> serverFixture,
        PostgresDatabaseFixture dbFixture,
        InMemoryFixture inMemoryFixture,
        JwtTokenIssuerFixture jwtTokenIssuerFixture,
        ITestOutputHelper outputHelper,
        StampAndRegistryFixture stampAndRegistryFixture)
        : base(
            serverFixture,
            dbFixture,
            inMemoryFixture,
            jwtTokenIssuerFixture,
            outputHelper,
            stampAndRegistryFixture)
    {
    }

    [Fact]
    public async Task Test1()
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
