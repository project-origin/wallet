using System;

namespace ProjectOrigin.Vault.Models;

public record Claim
{
    public required Guid Id { get; init; }
    public required Guid ProductionSliceId { get; init; }
    public required Guid ConsumptionSliceId { get; init; }
    public required ClaimState State { get; init; }
}

public record ClaimWithQuantity
{
    public required Guid Id { get; init; }
    public required Guid ProductionSliceId { get; init; }
    public required Guid ConsumptionSliceId { get; init; }
    public required ClaimState State { get; init; }
    public required long Quantity { get; init; }
    public required bool IsTrialClaim { get; init; }
}
