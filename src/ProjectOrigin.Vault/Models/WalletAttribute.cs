using System;
using System.Security.Cryptography;
using System.Text;

namespace ProjectOrigin.Vault.Models;

public record WalletAttribute
{
    public required Guid CertificateId { get; init; }
    public required string RegistryName { get; init; }
    public required string Key { get; init; }
    public required string Value { get; init; }
    public required byte[] Salt { get; init; }

    public string GetHashedValue()
    {
        var str = Key + Value + CertificateId.ToString() + Convert.ToHexString(Salt);
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(str)));
    }
}
