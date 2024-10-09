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
    public async Task InsertAndGetWithdrawnCursor()
    {
        var stampName = _fixture.Create<string>();
        var cursor = new WithdrawnCursor
        {
            StampName = stampName,
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
        var stampName = _fixture.Create<string>();
        var cursor = new WithdrawnCursor
        {
            StampName = stampName,
            SyncPosition = 1,
            LastSyncDate = DateTimeOffset.UtcNow.ToUtcTime()
        };
        await _withdrawnRepository.InsertWithdrawnCursor(cursor);

        var cursors = await _withdrawnRepository.GetWithdrawnCursors();

        var cursorToUpdate = cursors.First();
        cursorToUpdate.SyncPosition = 2;
        cursorToUpdate.LastSyncDate = DateTimeOffset.UtcNow.AddHours(2).ToUtcTime();

        await _withdrawnRepository.UpdateWithdrawnCursor(cursorToUpdate);
        var cursorsUpdated = await _withdrawnRepository.GetWithdrawnCursors();

        cursorsUpdated.Should().Contain(cursorToUpdate);
    }

    [Fact]
    public async Task UpdateWithdrawnCursor_WhenNotInsertedBeforeUpdate_InvalidOperationException()
    {
        var stampName = _fixture.Create<string>();
        var cursor = new WithdrawnCursor
        {
            StampName = stampName,
            SyncPosition = 1,
            LastSyncDate = DateTimeOffset.UtcNow.ToUtcTime()
        };

        var sut = () => _withdrawnRepository.UpdateWithdrawnCursor(cursor);

        await sut.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Withdrawn cursor with stamp name {stampName} could not be found");
    }
}
