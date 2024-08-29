using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record QueryCertificatesFilter
{
    public required string Owner { get; init; }
    public int Skip { get; init; } = 0;
    public int Limit { get; init; } = int.MaxValue;
    public string Sort { get; init; } = "ASC";
    public string SortBy { get; init; } = "Quantity";
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
    public GranularCertificateType? Type { get; init; }
}

public record QueryAggregatedCertificatesFilter : QueryCertificatesFilter
{
    public required TimeAggregate TimeAggregate { get; init; }
    public required string TimeZone { get; init; }
}
