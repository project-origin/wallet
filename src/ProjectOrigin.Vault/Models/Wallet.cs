using System;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.Vault.Models;

public record Wallet
{
    public required Guid Id { get; init; }
    public required string Owner { get; init; }
    public required IHDPrivateKey PrivateKey { get; init; }
    public DateTimeOffset? Disabled { get; init; }

    public bool IsDisabled()
    {
        return Disabled != null;
    }
}
