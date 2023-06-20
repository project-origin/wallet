
namespace ProjectOrigin.WalletSystem.Server.Models;

public record CertificateAttribute
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;

    public CertificateAttribute() { }

    public CertificateAttribute(string key, string value)
    {
        Key = key;
        Value = value;
    }
}
