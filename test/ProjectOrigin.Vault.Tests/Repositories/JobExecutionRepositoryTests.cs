using System;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Npgsql;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Repositories;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using Xunit;

namespace ProjectOrigin.Vault.Tests.Repositories;

public class JobExecutionRepositoryTests : AbstractRepositoryTests
{
    private readonly JobExecutionRepository _jobRepository;

    public JobExecutionRepositoryTests(PostgresDatabaseFixture dbFixture) : base(dbFixture)
    {
        _jobRepository = new JobExecutionRepository(_connection);
    }

    [Fact]
    public async Task GetLastExecutionTimeAsync_WhenJobDoesNotExist_ReturnsNull()
    {
        var jobName = Guid.NewGuid().ToString();
        var result = await _jobRepository.GetLastExecutionTime(jobName);
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateLastExecutionTimeAsync_WhenJobDoesNotExist_InsertsNewRecord()
    {
        var jobName = Guid.NewGuid().ToString();
        var executionTime = DateTimeOffset.Now.ToUtcTime();

        await _jobRepository.UpdateLastExecutionTime(jobName, executionTime);

        var result = await _jobRepository.GetLastExecutionTime(jobName);

        result.Should().Be(executionTime);
    }

    [Fact]
    public async Task UpdateLastExecutionTimeAsync_WhenJobExists_UpdatesExistingRecord()
    {
        var jobName = Guid.NewGuid().ToString();
        var executionTime = DateTimeOffset.Now.ToUtcTime();

        await _jobRepository.UpdateLastExecutionTime(jobName, executionTime);

        var result = await _jobRepository.GetLastExecutionTime(jobName);

        result.Should().Be(executionTime);

        var newExecutionTime = DateTimeOffset.Now.ToUtcTime().AddMinutes(5);

        await _jobRepository.UpdateLastExecutionTime(jobName, newExecutionTime);

        result = await _jobRepository.GetLastExecutionTime(jobName);

        result.Should().Be(newExecutionTime);
    }

    [Fact]
    public async Task AcquireAdvisoryLockAsync_WhenLockIsNotAcquired_ReturnsTrue()
    {
        var jobKey = _fixture.Create<int>();
        var result = await _jobRepository.AcquireAdvisoryLock(jobKey);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireAdvisoryLockAsync_WhenLockAcquiredBySameSession_ReturnsTrue()
    {
        var jobKey = _fixture.Create<int>();
        await _jobRepository.AcquireAdvisoryLock(jobKey);
        var result = await _jobRepository.AcquireAdvisoryLock(jobKey);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireAdvisoryLockAsync_WhenLockIsAcquiredByDifferentSession_ReturnsFalse()
    {
        var jobKey = _fixture.Create<int>();
        await _jobRepository.AcquireAdvisoryLock(jobKey);

        using (var session2 = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var repo = new JobExecutionRepository(session2);
            var result = await repo.AcquireAdvisoryLock(jobKey);
            result.Should().BeFalse();
        }
    }

    [Fact]
    public async Task ReleaseAdvisoryLockAsync_WhenLockIsAcquired_ReleasesLock()
    {
        var jobKey = _fixture.Create<int>();
        await _jobRepository.AcquireAdvisoryLock(jobKey);
        await _jobRepository.ReleaseAdvisoryLock(jobKey);

        using (var session2 = new NpgsqlConnection(_dbFixture.ConnectionString))
        {
            var repo = new JobExecutionRepository(session2);
            var result = await repo.AcquireAdvisoryLock(jobKey);
            result.Should().BeTrue();
        }
    }
}
