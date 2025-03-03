using System;

namespace ProjectOrigin.Vault.Models;

public enum RequestStatusState
{
    Pending = 1,
    Completed = 5,
    Failed = 9
}

public record RequestStatus
{
    public required Guid RequestId { get; init; }
    public required string Owner { get; init; }
    public required RequestStatusState Status { get; init; }
    public string? FailedReason { get; init; }
}
