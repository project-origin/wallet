using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace ProjectOrigin.WalletSystem.Server.Options;

public record JwtIssuer : IValidatableObject
{
    private Lazy<SecurityKey> securityKey;

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
        switch (Type.ToLowerInvariant())
        {
            case "ecdsa":
                var pem = File.ReadAllText(PemKeyFile);

                var ecdsa = ECDsa.Create();
                ecdsa.ImportFromPem(pem);
                return new ECDsaSecurityKey(ecdsa);
            default:
                throw new NotImplementedException($"Issuer key type {Type} not implemeted");
        }

    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ValidationResult result;
        try
        {
            var key = securityKey.Value;
            result = ValidationResult.Success!;
        }
        catch (Exception ex)
        {
            result = new ValidationResult(ex.Message);
        }

        yield return result;
    }
}
