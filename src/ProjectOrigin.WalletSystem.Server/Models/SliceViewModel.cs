using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record SliceViewModel
{
    public required Guid SliceId { get; init; }
    public required long Quantity { get; init; }
}
