using System.Diagnostics.Metrics;

namespace ProjectOrigin.Vault.Metrics;

public interface IClaimMetrics
{
    void IncrementClaimed();
    void IncrementRejected();
}

public class ClaimMetrics(MeterBase meterBase) : IClaimMetrics
{
    private readonly Counter<long> _claimsCount = meterBase.Meter.CreateCounter<long>(name: "po.vault.claim.certificate.count", unit: "{claim}");
    private readonly Counter<long> _claimsClaimedCount = meterBase.Meter.CreateCounter<long>(name: "po.vault.claim.certificate.claimed.count", unit: "{claim}");
    private readonly Counter<long> _claimsRejectedCount = meterBase.Meter.CreateCounter<long>(name: "po.vault.claim.certificate.rejected.count", unit: "{claim}");

    public void IncrementClaimed()
    {
        _claimsCount.Add(1);
        _claimsClaimedCount.Add(1);
    }

    public void IncrementRejected()
    {
        _claimsCount.Add(1);
        _claimsRejectedCount.Add(1);
    }
}
