using System;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
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
    public async Task UpdateLastExecutionTime_WhenCursorDoesNotExist_InsertsNewRecord()
    {
        var stampName = _fixture.Create<string>();
        var cursor = new WithdrawnCursor
        {
            StampName = stampName,
            SyncPosition = 0,
            LastSyncDate = DateTimeOffset.UtcNow.ToUtcTime()
        };
        await _withdrawnRepository.UpdateWithdrawnCursor(cursor);

        var cursors = await _withdrawnRepository.GetWithdrawnCursors();

        cursors.First(c => c.StampName == stampName).Should().BeEquivalentTo(cursor);
    }

    [Fact]
    public async Task UpdateWithdrawnCursor_WhenCursorExist_UpdateExistingCursor()
    {
        var stampName = _fixture.Create<string>();
        var cursor = new WithdrawnCursor
        {
            StampName = stampName,
            SyncPosition = 0,
            LastSyncDate = DateTimeOffset.UtcNow.ToUtcTime()
        };
        await _withdrawnRepository.UpdateWithdrawnCursor(cursor);

        var cursors = await _withdrawnRepository.GetWithdrawnCursors();

        var cursorToUpdate = cursors.First(c => c.StampName == stampName);
        cursorToUpdate.SyncPosition = 2;
        cursorToUpdate.LastSyncDate = DateTimeOffset.UtcNow.AddHours(2).ToUtcTime();

        await _withdrawnRepository.UpdateWithdrawnCursor(cursorToUpdate);
        var cursorsUpdated = await _withdrawnRepository.GetWithdrawnCursors();

        cursorsUpdated.First(c => c.StampName == stampName).Should().BeEquivalentTo(cursorToUpdate);
    }
}
