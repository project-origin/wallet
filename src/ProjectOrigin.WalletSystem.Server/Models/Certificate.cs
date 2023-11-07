using System;
using System.Collections.Generic;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record Certificate
{
    public required Guid Id { get; init; }
    public required string RegistryName { get; init; }
    public required DateTimeOffset StartDate { get; init; }
    public required DateTimeOffset EndDate { get; init; }
    public required string GridArea { get; init; } = string.Empty;
    public required GranularCertificateType CertificateType { get; init; }
    public List<CertificateAttribute> Attributes { get; init; } = new();
}
