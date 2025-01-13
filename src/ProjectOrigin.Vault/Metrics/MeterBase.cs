using System.Diagnostics.Metrics;

namespace ProjectOrigin.Vault.Metrics;

public class MeterBase(IMeterFactory meterFactory)
{
    public const string MeterName = "ProjectOrigin.Vault";
    public Meter Meter { get; } = meterFactory.Create(MeterName);
}
