using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public enum StatusState
{
    Pending = 1,
    Completed = 5,
    Failed = 9
}

public record RequestStatus
{
    public required Guid RequestId { get; init; }
    public required StatusState Status { get; init; }
}
