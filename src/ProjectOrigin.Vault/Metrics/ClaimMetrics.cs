using System.Diagnostics.Metrics;

namespace ProjectOrigin.Vault.Metrics;

public interface IClaimMetrics
{
    void IncrementClaimed();
    void IncrementClaimIntents();
}

public class ClaimMetrics(MeterBase meterBase) : IClaimMetrics
{
    private readonly Counter<long> _claimsClaimedCounter =
        meterBase.Meter.CreateCounter<long>(
            name: "po_vault_claim_certificate_claimed_count",
            unit: "{claim}",
            description: "The number of certificate claims successfully completed.");

    private readonly Counter<long> _claimIntentsCounter =
        meterBase.Meter.CreateCounter<long>(
            name: "po_vault_claim_certificate_intent_received_count",
            unit: "{claim}",
            description: "The total number of certificate claim intents received.");

    public void IncrementClaimed() => _claimsClaimedCounter.Add(1);
    public void IncrementClaimIntents() => _claimIntentsCounter.Add(1);
}
