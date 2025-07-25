using System.Diagnostics.Metrics;

namespace ProjectOrigin.Vault.Metrics;

public interface IClaimMetrics
{
    void IncrementClaimed();
    void IncrementClaimIntents();
    void IncrementFailedClaims();
    void IncrementTotalWattsClaimed(long quantity);
    void IncrementTotalTrialWattsClaimed(long quantity);
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

    private readonly Counter<long> _failedClaimsCounter =
        meterBase.Meter.CreateCounter<long>(
            name: "po_vault_claim_certificate_failed_count",
            unit: "{claim}",
            description: "The total number of failed certificate claims.");

    private readonly Counter<long> _totalWattsClaimedCounter =
        meterBase.Meter.CreateCounter<long>(
            name: "po_vault_claim_total_watts_claimed_count",
            unit: "{Watt hours}",
            description: "The total Watt hours that has been claimed.");

    private readonly Counter<long> _totalTrialWattsClaimedCounter =
        meterBase.Meter.CreateCounter<long>(
            name: "po_vault_trial_claim_total_watts_claimed_count",
            unit: "{Watt hours}",
            description: "The total trial Watt hours that has been claimed.");

    public void IncrementClaimed() => _claimsClaimedCounter.Add(1);
    public void IncrementClaimIntents() => _claimIntentsCounter.Add(1);
    public void IncrementFailedClaims() => _failedClaimsCounter.Add(1);
    public void IncrementTotalWattsClaimed(long quantity) => _totalWattsClaimedCounter.Add(quantity);
    public void IncrementTotalTrialWattsClaimed(long quantity) => _totalTrialWattsClaimedCounter.Add(quantity);
}
