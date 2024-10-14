using System.Threading.Tasks;
using System;
using System.Data;
using Dapper;

namespace ProjectOrigin.Vault.Repositories;

public interface IJobExecutionRepository
{
    Task<DateTimeOffset?> GetLastExecutionTime(string jobName);
    Task UpdateLastExecutionTime(string jobName, DateTimeOffset executionTime);
}

public class JobExecutionRepository : IJobExecutionRepository
{
    private readonly IDbConnection _connection;

    public JobExecutionRepository(IDbConnection connection) => this._connection = connection;


    public async Task<DateTimeOffset?> GetLastExecutionTime(string jobName)
    {
        var query = "SELECT last_execution_time FROM job_execution_history WHERE job_name = @jobName";
        return await _connection.QuerySingleOrDefaultAsync<DateTime?>(query, new { jobName = jobName });
    }

    public async Task UpdateLastExecutionTime(string jobName, DateTimeOffset executionTime)
    {
        var query = @"
            INSERT INTO job_execution_history (job_name, last_execution_time)
            VALUES (@JobName, @ExecutionTime)
            ON CONFLICT (job_name)
            DO UPDATE SET last_execution_time = EXCLUDED.last_execution_time";
        await _connection.ExecuteAsync(query, new { jobName, executionTime });
    }
}
