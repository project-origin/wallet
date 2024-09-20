using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace ProjectOrigin.Vault.Options;

public record JwtIssuer : IValidatableObject
{
    private readonly Lazy<SecurityKey> securityKey;

    public JwtIssuer()
    {
        securityKey = new(ImportKey);
    }

    public string IssuerName { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string PemKeyFile { get; init; } = string.Empty;
    public SecurityKey SecurityKey => securityKey.Value;

    private SecurityKey ImportKey()
    {
        if (!File.Exists(PemKeyFile))
            throw new FileNotFoundException($"Issuer key file ”{PemKeyFile}” not found");

        var pem = File.ReadAllText(PemKeyFile);
        switch (Type.ToLowerInvariant())
        {
            case "ecdsa":
                var ecdsa = ECDsa.Create();
                ecdsa.ImportFromPem(pem);
                return new ECDsaSecurityKey(ecdsa);
            case "rsa":
                var rsa = RSA.Create();
                rsa.ImportFromPem(pem);
                return new RsaSecurityKey(rsa);
            default:
                throw new NotImplementedException($"Issuer key type ”{Type}” not implemeted");
        }
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ValidationResult result;
        try
        {
            var _ = securityKey.Value;
            result = ValidationResult.Success!;
        }
        catch (Exception ex)
        {
            result = new ValidationResult($"Issuer key could not be imported as type ”{Type}”, {ex.Message}");
        }

        yield return result;
    }
}
