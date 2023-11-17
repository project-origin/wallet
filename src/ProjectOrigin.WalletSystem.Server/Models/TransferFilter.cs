using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record TransferFilter
{
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
}
