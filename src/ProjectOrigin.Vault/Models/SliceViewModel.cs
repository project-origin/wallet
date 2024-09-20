using System;

namespace ProjectOrigin.Vault.Models;

public record SliceViewModel
{
    public required Guid SliceId { get; init; }
    public required long Quantity { get; init; }
}
