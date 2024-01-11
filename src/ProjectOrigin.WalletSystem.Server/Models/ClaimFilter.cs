using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record ClaimFilter
{
    public required string Owner { get; init; }
    public int Skip { get; init; } = 0;
    public int Limit { get; init; } = int.MaxValue;
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
}
