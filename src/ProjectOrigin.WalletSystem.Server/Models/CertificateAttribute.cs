namespace ProjectOrigin.WalletSystem.Server.Models;

public record CertificateAttribute
{
    public required string Key { get; init; }
    public required string Value { get; init; }
    public required CertificateAttributeType Type { get; init; }

    public V1.Attribute ToProto()
    {
        return new V1.Attribute
        {
            Key = Key,
            Value = Value
        };
    }
}

public enum CertificateAttributeType
{
    ClearText = 0,
    Hashed = 1
}
