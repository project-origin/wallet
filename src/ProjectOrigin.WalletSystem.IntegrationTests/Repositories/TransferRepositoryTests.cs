using AutoFixture;
using FluentAssertions;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.IntegrationTests.TestExtensions;

namespace ProjectOrigin.WalletSystem.IntegrationTests.Repositories;

public class TransferRepositoryTests : AbstractRepositoryTests
{
    private readonly TransferRepository _transferRepository;

    public TransferRepositoryTests(PostgresDatabaseFixture dbFixture) : base(dbFixture)
    {
        _transferRepository = new TransferRepository(_connection);
    }

    [Fact]
    public async Task Test_GetTransfers()
    {
        // Arrange
        var issuestartDate = new DateTimeOffset(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var issueEndDate = new DateTimeOffset(2020, 6, 30, 12, 0, 0, TimeSpan.Zero);
        var queryStartDate = new DateTimeOffset(2020, 6, 8, 12, 0, 0, TimeSpan.Zero);
        var queryEndDate = new DateTimeOffset(2020, 6, 10, 12, 0, 0, TimeSpan.Zero);

        string subject = _fixture.Create<string>();

        for (int ownerNumber = 0; ownerNumber < 2; ownerNumber++)
        {
            subject = _fixture.Create<string>();

            for (int endpointNumber = 0; endpointNumber < 2; endpointNumber++)
            {
                var externalEndpoint = await _dbFixture.CreateExternalEndpoint(subject);

                for (DateTimeOffset i = issuestartDate; i < issueEndDate; i = i.AddHours(1))
                {
                    var certificate = await _dbFixture.CreateCertificate(
                        Guid.NewGuid(),
                        _fixture.Create<string>(),
                        GranularCertificateType.Production,
                        start: i,
                        end: i.AddHours(1));
                    await _dbFixture.CreateTransferredSlice(externalEndpoint, certificate, new PedersenCommitment.SecretCommitmentInfo(100));
                }
            }
        }

        // Act
        var result = await _transferRepository.QueryTransfers(new TransferFilter
        {
            Owner = subject,
            Start = queryStartDate,
            End = queryEndDate
        });

        //assert
        result.Items.Should().HaveCount(96);
        result.Items.Select(x => x.ReceiverId).Distinct().Should().HaveCount(2);
        result.Items.SelectMany(x => x.Attributes).Should().HaveCount(96 * 2);
        result.Items.Select(x => x.StartDate).Distinct().Should().HaveCount(48);
        result.Items.Min(x => x.StartDate).Should().Be(queryStartDate);
        result.Items.Select(x => x.EndDate).Distinct().Should().HaveCount(48);
        result.Items.Max(x => x.EndDate).Should().Be(queryEndDate);
    }


    [Theory]
    [InlineData("2020-06-08T12:00:00", "2020-06-12T12:00:00", TimeAggregate.Total, 2, 0, 1, 1)]
    [InlineData("2020-06-08T12:00:00", "2020-06-12T12:00:00", TimeAggregate.Day, 2, 2, 2, 5)]
    public async Task Test_QueryAggregatedTransfers(string from, string to, TimeAggregate aggregate, int take, int skip, int numberOfResults, int total)
    {

        // Arrange
        var issuestartDate = new DateTimeOffset(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var issueEndDate = new DateTimeOffset(2020, 6, 30, 12, 0, 0, TimeSpan.Zero);
        var queryStartDate = DateTimeOffset.Parse(from);
        var queryEndDate = DateTimeOffset.Parse(to);

        string subject = _fixture.Create<string>();

        for (int ownerNumber = 0; ownerNumber < 2; ownerNumber++)
        {
            subject = _fixture.Create<string>();

            for (int endpointNumber = 0; endpointNumber < 2; endpointNumber++)
            {
                var externalEndpoint = await _dbFixture.CreateExternalEndpoint(subject);

                for (DateTimeOffset i = issuestartDate; i < issueEndDate; i = i.AddHours(1))
                {
                    var certificate = await _dbFixture.CreateCertificate(
                        Guid.NewGuid(),
                        _fixture.Create<string>(),
                        GranularCertificateType.Production,
                        start: i,
                        end: i.AddHours(1));
                    await _dbFixture.CreateTransferredSlice(externalEndpoint, certificate, new PedersenCommitment.SecretCommitmentInfo(100));
                }
            }
        }

        // Act
        var result = await _transferRepository.QueryAggregatedTransfers(new TransferFilter
        {
            Owner = subject,
            Start = queryStartDate,
            End = queryEndDate,
            Limit = take,
            Skip = skip
        }, aggregate, "Europe/Copenhagen");

        //assert
        result.Items.Should().HaveCount(numberOfResults);
        result.Offset.Should().Be(skip);
        result.Limit.Should().Be(take);
        result.Count.Should().Be(numberOfResults);
        result.TotalCount.Should().Be(total);
    }
}
