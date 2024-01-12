using System;

namespace ProjectOrigin.WalletSystem.Server.ViewModels;

public record AggregatedTransferViewModel
{
    public required DateTimeOffset Start { get; init; }
    public required DateTimeOffset End { get; init; }
    public required long Quantity { get; init; }
}
