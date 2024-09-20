using System;

namespace ProjectOrigin.Vault.ViewModels;

public record AggregatedTransferViewModel
{
    public required DateTimeOffset Start { get; init; }
    public required DateTimeOffset End { get; init; }
    public required long Quantity { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

}
