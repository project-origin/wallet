using System;
using ProjectOrigin.Vault.Models;

namespace ProjectOrigin.Vault.ViewModels;

public record AggregatedCertificatesViewModel
{
    public required DateTimeOffset Start { get; init; }

    public required DateTimeOffset End { get; init; }

    public required long Quantity { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public required GranularCertificateType Type { get; init; }
}
