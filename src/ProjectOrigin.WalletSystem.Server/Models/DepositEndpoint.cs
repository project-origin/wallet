using System;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record DepositEndpoint
{
    public required Guid Id { get; init; }
    public required Guid? WalletId { get; init; }
    public required int? WalletPosition { get; init; }
    public required IHDPublicKey PublicKey { get; init; }
    public required string Owner { get; init; }
    public required string ReferenceText { get; init; }
    public required string Endpoint { get; init; }
}
