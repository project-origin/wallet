using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public interface IWithdrawnCursorRepository
{
    Task<IEnumerable<WithdrawnCursor>> GetWithdrawnCursors();
    Task InsertWithdrawnCursor(WithdrawnCursor withdrawnCursor);
    Task UpdateWithdrawnCursor(WithdrawnCursor withdrawnCursor);
}

public class WithdrawnCursorRepository : IWithdrawnCursorRepository
{
    private readonly IDbConnection _connection;

    public WithdrawnCursorRepository(IDbConnection connection) => this._connection = connection;

    public async Task<IEnumerable<WithdrawnCursor>> GetWithdrawnCursors()
    {
        using var gridReader = await _connection.QueryMultipleAsync("SELECT * FROM withdrawn_cursors");
        return gridReader.Read<WithdrawnCursor>();
    }

    public async Task InsertWithdrawnCursor(WithdrawnCursor withdrawnCursor)
    {
        await _connection.ExecuteAsync(
            @"INSERT INTO withdrawn_cursors(stamp_name, sync_position, last_sync_date)
              VALUES (@stampName, @syncPosition, @lastSyncDate)",
            new
            {
                withdrawnCursor.StampName,
                withdrawnCursor.SyncPosition,
                lastSyncDate = withdrawnCursor.LastSyncDate.ToUtcTime()
            });
    }

    public async Task UpdateWithdrawnCursor(WithdrawnCursor withdrawnCursor)
    {
        var rowsChanged = await _connection.ExecuteAsync(
            @"UPDATE withdrawn_cursors
              SET sync_position = @syncPosition, last_sync_date = @lastSyncDate
              WHERE stamp_name = @stampName",
            new
            {
                withdrawnCursor.StampName,
                withdrawnCursor.SyncPosition,
                lastSyncDate = withdrawnCursor.LastSyncDate.ToUtcTime()
            });

        if (rowsChanged != 1)
            throw new InvalidOperationException($"Withdrawn cursor with stamp name {withdrawnCursor.StampName} could not be found");
    }
}
