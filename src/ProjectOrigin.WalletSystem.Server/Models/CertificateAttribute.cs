
namespace ProjectOrigin.WalletSystem.Server.Models;

public record CertificateAttribute
{
    public required string Key { get; init; }
    public required string Value { get; init; }
}
