using ProjectOrigin.Vault.Models;
using System.Threading.Tasks;
using System;
using System.Data;
using Dapper;

namespace ProjectOrigin.Vault.Repositories;

public interface IRequestStatusRepository
{
    Task InsertRequestStatus(RequestStatus status);
    Task<RequestStatus?> GetRequestStatus(Guid requestId, string owner);
    Task SetRequestStatus(Guid requestId, string owner, RequestStatusState requestStatus, string? failedReason = null);
}

public class RequestStatusRepository : IRequestStatusRepository
{
    private readonly IDbConnection _connection;

    public RequestStatusRepository(IDbConnection connection) => this._connection = connection;

    public async Task InsertRequestStatus(RequestStatus status)
    {
        await _connection.ExecuteAsync(
            @"INSERT INTO request_statuses(request_id, owner, status, failed_reason, created, type)
              VALUES (@requestId, @owner, @status, @failedReason, @created, @type)",
            new
            {
                status.RequestId,
                status.Owner,
                status.Status,
                status.FailedReason,
                status.Created,
                status.Type
            });
    }

    public Task<RequestStatus?> GetRequestStatus(Guid requestId, string owner)
    {
        return _connection.QueryFirstOrDefaultAsync<RequestStatus>(
            @"SELECT s.*
              FROM request_statuses s
              WHERE s.request_id = @requestId
                AND s.owner = @owner",
            new
            {
                requestId,
                owner
            });
    }

    public async Task SetRequestStatus(Guid requestId, string owner, RequestStatusState status, string? failedReason = null)
    {
        var rowsChanged = await _connection.ExecuteAsync(
            @"UPDATE request_statuses
              SET status = @status,
                  failed_reason = @failedReason
              WHERE request_id = @requestId
                AND owner = @owner",
            new
            {
                requestId,
                owner,
                status,
                failedReason
            });

        if (rowsChanged != 1)
            throw new InvalidOperationException($"Transfer request with id {requestId} could not be found");
    }
}
