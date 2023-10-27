using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record ClaimViewModel
{
    public required Guid Id { get; init; }
    public required uint Quantity { get; init; }

    public required string ProductionRegistryName { get; init; }
    public required Guid ProductionCertificateId { get; init; }
    public required DateTimeOffset ProductionStart { get; init; }
    public required DateTimeOffset ProductionEnd { get; init; }
    public required string ProductionGridArea { get; init; }
    public List<CertificateAttribute> ProductionAttributes { get; init; } = new();

    public required string ConsumptionRegistryName { get; init; }
    public required Guid ConsumptionCertificateId { get; init; }
    public required DateTimeOffset ConsumptionStart { get; init; }
    public required DateTimeOffset ConsumptionEnd { get; init; }
    public required string ConsumptionGridArea { get; init; }
    public List<CertificateAttribute> ConsumptionAttributes { get; init; } = new();

    public V1.Claim ToProto()
    {
        return new V1.Claim
        {
            ClaimId = new Common.V1.Uuid { Value = Id.ToString() },
            Quantity = Quantity,
            ProductionCertificate = new V1.Claim.Types.ClaimCertificateInfo
            {
                FederatedId = new Common.V1.FederatedStreamId
                {
                    Registry = ProductionRegistryName,
                    StreamId = new Common.V1.Uuid { Value = ProductionCertificateId.ToString() }
                },
                Start = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(ProductionStart),
                End = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(ProductionEnd),
                GridArea = ProductionGridArea,
                Attributes = { ProductionAttributes.Select(att => att.ToProto()) }
            },
            ConsumptionCertificate = new V1.Claim.Types.ClaimCertificateInfo
            {
                FederatedId = new Common.V1.FederatedStreamId
                {
                    Registry = ConsumptionRegistryName,
                    StreamId = new Common.V1.Uuid { Value = ConsumptionCertificateId.ToString() }
                },
                Start = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(ConsumptionStart),
                End = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(ConsumptionEnd),
                GridArea = ConsumptionGridArea,
                Attributes = { ConsumptionAttributes.Select(att => att.ToProto()) }
            }
        };
    }
}
