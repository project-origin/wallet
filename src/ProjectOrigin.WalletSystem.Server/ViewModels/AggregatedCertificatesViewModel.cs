using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.ViewModels;

public record AggregatedCertificatesViewModel
{
    public required long Start { get; init; }

    public required long End { get; init; }

    public required long Quantity { get; init; }

    public required GranularCertificateType Type { get; init; }

}
