using System.Diagnostics.Metrics;

namespace ProjectOrigin.Vault.Metrics;

public interface ITransferMetrics
{
    void IncrementCompleted();
    void IncrementTransferIntents();
}

public class TransferMetrics(MeterBase meterBase) : ITransferMetrics
{
    private readonly Counter<long> _transferIntentsCounter =
        meterBase.Meter.CreateCounter<long>(
            name: "po_vault_transfer_intent_count",
            unit: "{transfer}",
            description: "The number of certificate transfer intents received.");

    private readonly Counter<long> _transfersCompletedCounter =
        meterBase.Meter.CreateCounter<long>(
            name: "po_vault_transfer_completed_count",
            unit: "{transfer}",
            description: "The number of certificate transfers successfully completed.");

    public void IncrementCompleted() => _transfersCompletedCounter.Add(1);

    public void IncrementTransferIntents() => _transferIntentsCounter.Add(1);
}