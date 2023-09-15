using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record Slice
{
    public required Guid Id { get; init; }
    public required Guid DepositEndpointId { get; init; }
    public required int DepositEndpointPosition { get; init; }
    public required string Registry { get; init; }
    public required Guid CertificateId { get; init; }
    public required long Quantity { get; init; }
    public required byte[] RandomR { get; init; }
    public required SliceState SliceState { get; init; }
}
