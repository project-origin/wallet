namespace ProjectOrigin.Vault.Projections;

public record AllocationSlice : CertificateSlice
{
    public required Common.V1.Uuid AllocationId { get; init; }
    public required Common.V1.FederatedStreamId ProductionCertificateId { get; init; }
    public required Common.V1.FederatedStreamId ConsumptionCertificateId { get; init; }
}

public record CertificateSlice
{
    public required Electricity.V1.Commitment Commitment { get; init; }
    public required Electricity.V1.PublicKey Owner { get; init; }
}
