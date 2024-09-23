using System;

namespace ProjectOrigin.Vault.Models;

public record TransferredSlice : AbstractSlice
{
    public required Guid ExternalEndpointId { get; init; }
    public required int ExternalEndpointPosition { get; init; }
    public required TransferredSliceState State { get; init; }
}

public enum TransferredSliceState
{
    Registering = 3,
    Transferred = 5,
}
