using System.Diagnostics.Metrics;

namespace ProjectOrigin.Vault.Metrics;

public class TransferMetrics
{
    private const string MetricName = "ProjectOrigin.Vault.Transfer";
    private readonly Counter<long> _transferCounter;

    public TransferMetrics()
    {
        var meter = new Meter(MetricName);
        _transferCounter = meter.CreateCounter<long>("po_vault_transfer_count", "transfers", "Total number of transfers processed.");
    }

    public void UpdateMetric()
    {
        _transferCounter.Add(1);
    }
}
