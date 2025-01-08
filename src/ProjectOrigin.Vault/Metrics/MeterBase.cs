using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;

namespace ProjectOrigin.Vault.Metrics;

public class MeterBase(IMeterFactory meterFactory, IConfiguration configuration)
{
    public const string MeterName = "ProjectOrigin.Vault";
    public Meter Meter { get; } = meterFactory.Create(configuration["VaultMeterName"] ?? MeterName);
}
