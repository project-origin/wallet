using System;
using System.Threading.Tasks;
using FluentAssertions;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Repositories;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using Xunit;

namespace ProjectOrigin.Vault.Tests.Repositories;

public class OutboxMessageRepositoryTests : AbstractRepositoryTests
{
    private readonly OutboxMessageRepository _repository;

    public OutboxMessageRepositoryTests(PostgresDatabaseFixture dbFixture) : base(dbFixture)
    {
        _repository = new OutboxMessageRepository(_connection);
    }

    [Fact]
    public async Task Delete()
    {
        var message = new OutboxMessage
        {
            Created = DateTimeOffset.Now.ToUtcTime(),
            JsonPayload = "{}",
            MessageType = "Test",
            Id = Guid.NewGuid()
        };

        await _repository.Create(message);

        var queriedMessage = await _repository.GetFirst();
        queriedMessage.Should().BeEquivalentTo(message);

        await _repository.Delete(queriedMessage!.Id);

        var deletedMessage = await _repository.GetFirst();
        deletedMessage.Should().BeNull();
    }
}
