using System.Diagnostics.Metrics;

namespace ProjectOrigin.Vault.Metrics;

public interface ITransferMetrics
{
    void IncrementCompleted();
    void IncrementRejected();
}

public class TransferMetrics(MeterBase meterBase) : ITransferMetrics
{
    private readonly Counter<long> _transferCount = meterBase.Meter.CreateCounter<long>(name: "po.vault.transfer.count", unit: "{transfer}");
    private readonly Counter<long> _transferCompletedCount = meterBase.Meter.CreateCounter<long>(name: "po.vault.transfer.completed.count", unit: "{transfer}");
    private readonly Counter<long> _transferRejectedCount = meterBase.Meter.CreateCounter<long>(name: "po.vault.transfer.rejected.count", unit: "{transfer}");

    public void IncrementCompleted()
    {
        _transferCount.Add(1);
        _transferCompletedCount.Add(1);
    }

    public void IncrementRejected()
    {
        _transferCount.Add(1);
        _transferRejectedCount.Add(1);
    }
}
