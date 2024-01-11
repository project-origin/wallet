
using System;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.ViewModels;

public sealed record AttributeViewModel : CertificateAttribute
{
    public required string RegistryName { get; init; }
    public required Guid CertificateId { get; init; }
}
