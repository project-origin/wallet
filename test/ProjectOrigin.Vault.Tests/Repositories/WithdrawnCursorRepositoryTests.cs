using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Repositories;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using Xunit;

namespace ProjectOrigin.Vault.Tests.Repositories;

public class WithdrawnCursorRepositoryTests : AbstractRepositoryTests
{
    private readonly WithdrawnCursorRepository _withdrawnRepository;

    public WithdrawnCursorRepositoryTests(PostgresDatabaseFixture dbFixture) : base(dbFixture)
    {
        _withdrawnRepository = new WithdrawnCursorRepository(_connection);
    }

    [Fact]
    public async Task InsertAndGetWithdrawnCursor()
    {
        var cursor = new WithdrawnCursor
        {
            StampName = "Narnia_stamp",
            SyncPosition = 1,
            LastSyncDate = DateTimeOffset.UtcNow.ToUtcTime()
        };
        await _withdrawnRepository.InsertWithdrawnCursor(cursor);

        var cursors = await _withdrawnRepository.GetWithdrawnCursors();

        cursors.Count().Should().Be(1);
        cursors.First().Should().BeEquivalentTo(cursor);
    }

    [Fact]
    public async Task UpdateWithdrawnCursor()
    {
        var cursor = new WithdrawnCursor
        {
            StampName = "Narnia_stamp",
            SyncPosition = 1,
            LastSyncDate = DateTimeOffset.UtcNow.ToUtcTime()
        };
        await _withdrawnRepository.InsertWithdrawnCursor(cursor);

        var cursors = await _withdrawnRepository.GetWithdrawnCursors();

        var cursorToUpdate = cursors.First();
        cursorToUpdate.SyncPosition = 2;
        cursorToUpdate.LastSyncDate = DateTimeOffset.UtcNow.AddHours(2).ToUtcTime();

        await _withdrawnRepository.UpdateWithdrawnCursor(cursorToUpdate);
        var cursorUpdated = await _withdrawnRepository.GetWithdrawnCursors();

        cursorUpdated.Count().Should().Be(1);
        cursorUpdated.First().Should().BeEquivalentTo(cursorToUpdate);
    }

    [Fact]
    public async Task UpdateWithdrawnCursor_WhenNotInsertedBeforeUpdate_InvalidOperationException()
    {
        var cursor = new WithdrawnCursor
        {
            StampName = "Narnia_stamp",
            SyncPosition = 1,
            LastSyncDate = DateTimeOffset.UtcNow.ToUtcTime()
        };

        var sut = () => _withdrawnRepository.UpdateWithdrawnCursor(cursor);

        await sut.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Withdrawn cursor with stamp name Narnia_stamp could not be found");
    }
}
