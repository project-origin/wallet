using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record ClaimFilter
{
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
}
