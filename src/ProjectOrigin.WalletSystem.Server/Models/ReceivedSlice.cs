using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record ReceivedSlice : BaseSlice
{
    public required Guid ReceiveEndpointId { get; init; }
    public required int ReceiveEndpointPosition { get; init; }
    public required ReceivedSliceState SliceState { get; init; }
}

public enum ReceivedSliceState
{
    Available = 1,
    Slicing = 2, // Reserved
    Registering = 3,
    Sliced = 4,
    Claimed = 7,
    Reserved = 10,
}

