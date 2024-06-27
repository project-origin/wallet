using System;

namespace ProjectOrigin.WalletSystem.Server.ViewModels;

public record AggregatedClaimViewModel
{
    public required DateTimeOffset Start { get; init; }
    public required DateTimeOffset End { get; init; }
    public required long Quantity { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

}
