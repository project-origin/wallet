using ProjectOrigin.Vault.Tests.TestClassFixtures;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Repositories;
using System.Threading.Tasks;
using System;
using FluentAssertions;
using ProjectOrigin.Vault.Extensions;
using Xunit;

namespace ProjectOrigin.Vault.Tests.Repositories;

public class RequestStatusRepositoryTests : AbstractRepositoryTests
{
    private readonly RequestStatusRepository _requestStatusRepository;

    public RequestStatusRepositoryTests(PostgresDatabaseFixture dbFixture) : base(dbFixture)
    {
        _requestStatusRepository = new RequestStatusRepository(_connection);
    }

    [Fact]
    public async Task CreateAndGetRequestStatus()
    {
        var status = new RequestStatus
        {
            RequestId = Guid.NewGuid(),
            Owner = Guid.NewGuid().ToString(),
            Status = RequestStatusState.Pending,
            FailedReason = "Test failed message",
            Created = DateTimeOffset.Now.ToUtcTime(),
            Type = RequestStatusType.Claim
        };

        await _requestStatusRepository.InsertRequestStatus(status);

        var queriedStatus = await _requestStatusRepository.GetRequestStatus(status.RequestId, status.Owner);

        queriedStatus.Should().BeEquivalentTo(status);
    }

    [Fact]
    public async Task SetRequestStatus()
    {
        var status = new RequestStatus
        {
            RequestId = Guid.NewGuid(),
            Owner = Guid.NewGuid().ToString(),
            Status = RequestStatusState.Pending,
            Created = DateTimeOffset.Now.ToUtcTime(),
            Type = RequestStatusType.Transfer
        };

        await _requestStatusRepository.InsertRequestStatus(status);

        await _requestStatusRepository.SetRequestStatus(status.RequestId, status.Owner, RequestStatusState.Failed, "Test failed message");

        var queriedStatus = await _requestStatusRepository.GetRequestStatus(status.RequestId, status.Owner);

        queriedStatus.Should().BeEquivalentTo(status with { Status = RequestStatusState.Failed, FailedReason = "Test failed message" });
    }
}
