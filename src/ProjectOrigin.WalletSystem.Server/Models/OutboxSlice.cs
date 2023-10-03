using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record OutboxSlice : BaseSlice
{
    public required Guid OutboxEndpointId { get; init; }
    public required int OutboxEndpointPosition { get; init; }
    public required OutboxSliceState SliceState { get; init; }
}

public enum OutboxSliceState
{
    Registering = 3,
    Transferred = 5,
}
