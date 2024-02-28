using System;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.ViewModels;

public record AggregatedCertificatesViewModel
{
    public required DateTimeOffset Start { get; init; }

    public required DateTimeOffset End { get; init; }

    public required long Quantity { get; init; }

    public required GranularCertificateType Type { get; init; }
}
