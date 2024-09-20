using System;

namespace ProjectOrigin.Vault.Models;

public record WalletSlice : AbstractSlice
{
    public required Guid WalletEndpointId { get; init; }
    public required int WalletEndpointPosition { get; init; }
    public required WalletSliceState State { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public enum WalletSliceState
{
    Available = 1,
    Slicing = 2, // Reserved
    Registering = 3,
    Sliced = 4,
    Claimed = 7,
    Reserved = 10,
}
