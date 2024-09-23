using ProjectOrigin.WalletSystem.Server.Models;
using System;
using System.Collections.Generic;

namespace ProjectOrigin.WalletSystem.Server.ViewModels;

public record CertificateViewModel
{
    public required Guid CertificateId { get; init; }
    public required string RegistryName { get; init; }
    public required GranularCertificateType CertificateType { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
    public required string GridArea { get; init; }
    public required DateTimeOffset StartDate { get; init; }
    public required DateTimeOffset EndDate { get; init; }
    public required uint Quantity { get; init; }
    public required bool Withdrawn { get; init; }
    public List<CertificateAttribute> Attributes { get; } = new();
}
