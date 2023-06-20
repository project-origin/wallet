using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record SliceViewModel
{
    public Guid SliceId { get; init; }
    public long Quantity { get; init; }
}
