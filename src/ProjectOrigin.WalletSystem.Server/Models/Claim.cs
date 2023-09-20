using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record Claim
{
    public required Guid Id { get; init; }
    public required Guid ProductionSliceId { get; init; }
    public required Guid ConsumptionSliceId { get; init; }
    public required ClaimState State { get; init; }
}
