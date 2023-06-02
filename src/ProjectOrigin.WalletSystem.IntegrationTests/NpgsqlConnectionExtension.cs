using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Dapper;
using Npgsql;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public static class NpgsqlConnectionExtension
{
    public static async Task<T> RepeatedlyQueryFirstOrDefaultUntil<T>(this NpgsqlConnection connection, string sql, object? param = null, TimeSpan? timeLimit = null)
    {
        var limit = timeLimit ?? TimeSpan.FromSeconds(15);

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        do
        {
            T entity = await connection.QueryFirstOrDefaultAsync<T>(sql, param);
            if (entity != null)
                return entity;

            await Task.Delay(TimeSpan.FromMicroseconds(100));
        } while (stopwatch.Elapsed < limit);

        throw new Exception($"Entity not found within the time limit ({limit.TotalSeconds} seconds)");
    }
}
