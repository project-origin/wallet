using System;

namespace ProjectOrigin.Vault.Models;

public enum RequestStatusState
{
    Pending = 1,
    Completed = 5,
    Failed = 9
}

public enum RequestStatusType
{
    Unknown = 0, //Only used for old request statuses before this type was introduced. On these, this is the default value (see v2-0012.sql)
    Claim = 1,
    Transfer = 2
}

public record RequestStatus
{
    public required Guid RequestId { get; init; }
    public required string Owner { get; init; }
    public required RequestStatusState Status { get; init; }
    public required RequestStatusType Type { get; init; }
    public required DateTimeOffset Created { get; init; }
    public string? FailedReason { get; init; }
}
