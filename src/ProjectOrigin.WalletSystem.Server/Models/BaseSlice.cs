using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public abstract record BaseSlice
{
    public required Guid Id { get; init; }
    public required string RegistryName { get; init; }
    public required Guid CertificateId { get; init; }
    public required long Quantity { get; init; }
    public required byte[] RandomR { get; init; }
}
