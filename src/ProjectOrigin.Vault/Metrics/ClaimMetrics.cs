using System.Diagnostics.Metrics;

namespace ProjectOrigin.Vault.Metrics;

public class ClaimMetrics
{
    private const string MetricName = "ProjectOrigin.Vault.Claim";
    private readonly Counter<long> _claimsCounter;

    public ClaimMetrics()
    {
        var meter = new Meter(MetricName);
        _claimsCounter = meter.CreateCounter<long>("po_vault_claim_count", "claims", "Total number of claims processed.");
    }

    public void UpdateMetric()
    {
        _claimsCounter.Add(1);
    }
}
