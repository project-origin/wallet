using System;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record Wallet
{
    public required Guid Id { get; init; }
    public required string Owner { get; init; }
    public required IHDPrivateKey PrivateKey { get; init; }
}
