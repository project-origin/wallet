using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record WalletSlice : BaseSlice
{
    public required Guid WalletEndpointId { get; init; }
    public required int WalletEndpointPosition { get; init; }
    public required WalletSliceState SliceState { get; init; }
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
