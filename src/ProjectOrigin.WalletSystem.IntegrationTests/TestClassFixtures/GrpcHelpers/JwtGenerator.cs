using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

public class JwtGenerator
{
    private readonly ECDsa ecdsa;
    private readonly string issuer;
    private readonly string audience;
    private readonly int expirationMinutes;

    public JwtGenerator(string issuer = "", string audience = "", int expirationMinutes = 5)
    {
        this.issuer = issuer;
        this.audience = audience;
        this.expirationMinutes = expirationMinutes;

        ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    }

    public string GenerateToken(string subject, string name)
    {
        var claims = new[]
        {
            new Claim("sub", subject),
            new Claim("name", name),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), System.Security.Claims.ClaimValueTypes.Integer64),
        };

        var key = new ECDsaSecurityKey(ecdsa);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.EcdsaSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
