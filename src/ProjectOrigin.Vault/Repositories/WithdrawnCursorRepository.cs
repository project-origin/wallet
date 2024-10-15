using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Models;

namespace ProjectOrigin.Vault.Repositories;

public interface IWithdrawnCursorRepository
{
    Task<IEnumerable<WithdrawnCursor>> GetWithdrawnCursors();
    Task UpdateWithdrawnCursor(WithdrawnCursor withdrawnCursor);
}

public class WithdrawnCursorRepository : IWithdrawnCursorRepository
{
    private readonly IDbConnection _connection;

    public WithdrawnCursorRepository(IDbConnection connection) => _connection = connection;

    public async Task<IEnumerable<WithdrawnCursor>> GetWithdrawnCursors()
    {
        return await _connection.QueryAsync<WithdrawnCursor>("SELECT * FROM withdrawn_cursors");
    }

    public async Task UpdateWithdrawnCursor(WithdrawnCursor withdrawnCursor)
    {
        var sql = @"
            INSERT INTO withdrawn_cursors (stamp_name, sync_position, last_sync_date)
            VALUES (@stampName, @syncPosition, @lastSyncDate)
            ON CONFLICT (stamp_name)
            DO UPDATE SET sync_position = EXCLUDED.sync_position, last_sync_date = EXCLUDED.last_sync_date";

        var rowsChanged = await _connection.ExecuteAsync(sql,
            new
            {
                stampName = withdrawnCursor.StampName,
                syncPosition = withdrawnCursor.SyncPosition,
                lastSyncDate = withdrawnCursor.LastSyncDate.ToUtcTime()
            });
    }
}
