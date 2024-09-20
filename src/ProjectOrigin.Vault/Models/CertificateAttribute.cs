namespace ProjectOrigin.Vault.Models;

public record CertificateAttribute
{
    public required string Key { get; init; }
    public required string Value { get; init; }
    public required CertificateAttributeType Type { get; init; }
}

public enum CertificateAttributeType
{
    ClearText = 0,
    Hashed = 1
}
