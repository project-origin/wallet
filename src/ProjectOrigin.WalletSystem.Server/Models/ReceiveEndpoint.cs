using System;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record ReceiveEndpoint
{
    public required Guid Id { get; init; }
    public required Guid WalletId { get; init; }
    public required int WalletPosition { get; init; }
    public required IHDPublicKey PublicKey { get; init; }
    public required bool IsRemainderEndpoint { get; init; }
}
