using ProjectOrigin.WalletSystem.Server.Models;
using System.Threading.Tasks;
using System;
using System.Data;
using Dapper;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public interface IRequestStatusRepository
{
    Task InsertRequestStatus(RequestStatus status);
    Task<RequestStatus?> GetRequestStatus(Guid requestId);
    Task SetRequestStatus(Guid requestId, StatusState status);
}

public class RequestStatusRepository : IRequestStatusRepository
{
    private readonly IDbConnection _connection;

    public RequestStatusRepository(IDbConnection connection) => this._connection = connection;

    public async Task InsertRequestStatus(RequestStatus status)
    {
        await _connection.ExecuteAsync(
            @"INSERT INTO request_statuses(request_id, status)
              VALUES (@requestId, @status)",
            new
            {
                status.RequestId,
                status.Status
            });
    }

    public Task<RequestStatus?> GetRequestStatus(Guid requestId)
    {
        return _connection.QueryFirstOrDefaultAsync<RequestStatus>(
            @"SELECT s.*
              FROM request_statuses s
              WHERE s.request_id = @requestId",
            new
            {
                requestId
            });
    }

    public async Task SetRequestStatus(Guid requestId, StatusState status)
    {
        var rowsChanged = await _connection.ExecuteAsync(
            @"UPDATE request_statuses
              SET status = @status
              WHERE request_id = @requestId",
            new
            {
                requestId,
                status
            });

        if (rowsChanged != 1)
            throw new InvalidOperationException($"Transfer request with id {requestId} could not be found");
    }
}
