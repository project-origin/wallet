using System.Diagnostics.Metrics;

namespace ProjectOrigin.Vault.Metrics;

public class VaultMetrics
{
    private const string MetricName = "VaultMetrics";
    private readonly Meter _meter;
    private readonly Counter<long> _claimsCounter;

    public VaultMetrics()
    {
        _meter = new Meter(MetricName);
        _claimsCounter = _meter.CreateCounter<long>("vault_claims_total", "claims", "Total number of claims processed.");
    }

    public void IncrementClaims()
    {
        _claimsCounter.Add(1);
    }
}
