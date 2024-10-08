using System;

namespace ProjectOrigin.Vault.Models;

public record QueryTransfersFilter
{
    public required string Owner { get; init; }
    public int Skip { get; init; } = 0;
    public int Limit { get; init; } = int.MaxValue;
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
}

public record QueryTransfersFilterCursor
{
    public required string Owner { get; init; }
    public int Limit { get; init; } = int.MaxValue;
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
    public DateTimeOffset? UpdatedSince { get; init; }
}

public record QueryAggregatedTransfersFilter : QueryTransfersFilter
{
    public required TimeAggregate TimeAggregate { get; init; }
    public required string TimeZone { get; init; }
}
