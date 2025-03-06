using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.Vault.Models;

namespace ProjectOrigin.Vault.Repositories;

public interface IOutboxMessageRepository
{
    Task Create(OutboxMessage message);
    Task<OutboxMessage?> GetFirst();
    Task Delete(Guid outboxMessageId);
}

public class OutboxMessageRepository : IOutboxMessageRepository
{
    private readonly IDbConnection _connection;

    public OutboxMessageRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task Create(OutboxMessage message)
    {
        await _connection.ExecuteAsync(
            @"INSERT INTO OutboxMessages(id, message_type, json_payload, created)
              VALUES (@Id, @MessageType, @JsonPayload, @Created)",
            new
            {
                message.Id,
                message.MessageType,
                message.JsonPayload,
                message.Created
            });
    }

    public Task<OutboxMessage?> GetFirst()
    {
        return _connection.QueryFirstOrDefaultAsync<OutboxMessage>(
            """
            SELECT
                id, message_type, json_payload, created
            FROM
                OutboxMessages
            FOR UPDATE SKIP LOCKED
            LIMIT 1
            """
            );
    }

    public async Task Delete(Guid outboxMessageId)
    {
        var rowsChanged = await _connection.ExecuteAsync(
            @"DELETE FROM OutboxMessages
                WHERE id = @Id",
            new
            {
                Id = outboxMessageId
            });

        if (rowsChanged != 1)
            throw new InvalidOperationException($"OutboxMessage with id {outboxMessageId} could not be found");
    }
}
