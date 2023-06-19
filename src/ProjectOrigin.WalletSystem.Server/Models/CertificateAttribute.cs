
namespace ProjectOrigin.WalletSystem.Server.Models;

public class CertificateAttribute
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public CertificateAttribute() { }

    public CertificateAttribute(string key, string value)
    {
        Key = key;
        Value = value;
    }
    public override bool Equals(object? obj)
    {
        if (obj == null)
            return false;

        var b = obj as CertificateAttribute;
        return Key.Equals(b!.Key) && Value.Equals(b!.Value);
    }
    public override int GetHashCode()
    {
        return Key.GetHashCode() * Value.GetHashCode();
    }
}
