using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;
using System.Threading.Tasks;
using System;
using FluentAssertions;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.Repositories;

public class RequestStatusRepositoryTests : AbstractRepositoryTests
{
    private readonly RequestStatusRepository _requestStatusRepository;

    public RequestStatusRepositoryTests(PostgresDatabaseFixture dbFixture) : base(dbFixture)
    {
        _requestStatusRepository = new RequestStatusRepository(_connection);
    }

    [Fact]
    public async Task CreateAndGetTransferStatus()
    {
        var status = new RequestStatus
        {
            RequestId = Guid.NewGuid(),
            Owner = Guid.NewGuid().ToString(),
            Status = RequestStatusState.Pending,
            FailedReason = "Test failed message"
        };

        await _requestStatusRepository.InsertRequestStatus(status);

        var queriedStatus = await _requestStatusRepository.GetRequestStatus(status.RequestId, status.Owner);

        queriedStatus.Should().BeEquivalentTo(status);
    }

    [Fact]
    public async Task SetTransferStatus()
    {
        var status = new RequestStatus
        {
            RequestId = Guid.NewGuid(),
            Owner = Guid.NewGuid().ToString(),
            Status = RequestStatusState.Pending
        };

        await _requestStatusRepository.InsertRequestStatus(status);

        await _requestStatusRepository.SetRequestStatus(status.RequestId, status.Owner, RequestStatusState.Failed, "Test failed message");

        var queriedStatus = await _requestStatusRepository.GetRequestStatus(status.RequestId, status.Owner);

        queriedStatus.Should().BeEquivalentTo(status with { Status = RequestStatusState.Failed, FailedReason = "Test failed message" });
    }
}
