using System;

namespace ProjectOrigin.Vault.Models;

public record QueryCertificatesFilterCursor
{
    public required string Owner { get; init; }
    public required DateTimeOffset? UpdatedSince { get; init; }
    public int Limit { get; init; } = int.MaxValue;
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
    public GranularCertificateType? Type { get; init; }
}
