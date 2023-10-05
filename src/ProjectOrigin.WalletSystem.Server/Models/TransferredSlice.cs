using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record TransferredSlice : BaseSlice
{
    public required Guid ExternalEndpointId { get; init; }
    public required int ExternalEndpointPosition { get; init; }
    public required TransferredSliceState SliceState { get; init; }
}

public enum TransferredSliceState
{
    Registering = 3,
    Transferred = 5,
}
