using System;
using ProjectOrigin.WalletSystem.Server.Services.REST.v1;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record CertificatesFilter(SliceState State)
{
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
    public GranularCertificateType? Type { get; init; }
}
