using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record CertificatesFilter
{
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
    public GranularCertificateType? Type { get; init; }
}
