using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record DepositSlice : BaseSlice
{
    public required Guid DepositEndpointId { get; init; }
    public required int DepositEndpointPosition { get; init; }
    public required DepositSliceState SliceState { get; init; }
}

public enum DepositSliceState
{
    Registering = 3,
    Transferred = 5,
}
