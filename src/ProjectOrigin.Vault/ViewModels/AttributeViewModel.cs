
using System;
using ProjectOrigin.Vault.Models;

namespace ProjectOrigin.Vault.ViewModels;

public sealed record AttributeViewModel : CertificateAttribute
{
    public required string RegistryName { get; init; }
    public required Guid CertificateId { get; init; }
}
