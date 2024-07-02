using System;
using System.Collections.Generic;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.ViewModels;

public record ClaimViewModel
{
    public required Guid ClaimId { get; init; }
    public required uint Quantity { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }

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
}
