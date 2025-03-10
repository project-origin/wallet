using System;

namespace ProjectOrigin.Vault.Models;

public record OutboxMessage
{
    public required Guid Id { get; init; }
    public required string MessageType { get; init; }
    public required string JsonPayload { get; init; }
    public required DateTimeOffset Created { get; init; }
}
