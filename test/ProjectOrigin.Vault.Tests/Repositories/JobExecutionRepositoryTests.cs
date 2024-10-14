using System;
using System.Threading.Tasks;
using FluentAssertions;
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
        var result = await _jobRepository.GetLastExecutionTimeAsync(jobName);
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateLastExecutionTimeAsync_WhenJobDoesNotExist_InsertsNewRecord()
    {
        var jobName = Guid.NewGuid().ToString();
        var executionTime = DateTimeOffset.Now.ToUtcTime();

        await _jobRepository.UpdateLastExecutionTimeAsync(jobName, executionTime);

        var result = await _jobRepository.GetLastExecutionTimeAsync(jobName);

        result.Should().Be(executionTime);
    }

    [Fact]
    public async Task UpdateLastExecutionTimeAsync_WhenJobExists_UpdatesExistingRecord()
    {
        var jobName = Guid.NewGuid().ToString();
        var executionTime = DateTimeOffset.Now.ToUtcTime();

        await _jobRepository.UpdateLastExecutionTimeAsync(jobName, executionTime);

        var result = await _jobRepository.GetLastExecutionTimeAsync(jobName);

        result.Should().Be(executionTime);

        var newExecutionTime = DateTimeOffset.Now.ToUtcTime().AddMinutes(5);

        await _jobRepository.UpdateLastExecutionTimeAsync(jobName, newExecutionTime);

        result = await _jobRepository.GetLastExecutionTimeAsync(jobName);

        result.Should().Be(newExecutionTime);
    }
}
