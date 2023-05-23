using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

public class JwtGenerator
{
    private readonly ECDsa _ecdsa;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;

    public JwtGenerator(string issuer = "", string audience = "", int expirationMinutes = 5)
    {
        _issuer = issuer;
        _audience = audience;
        _expirationMinutes = expirationMinutes;

        _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    }

    public string GenerateToken(string subject, string name)
    {
        var claims = new[]
        {
            new Claim("sub", subject),
            new Claim("name", name),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };

        var key = new ECDsaSecurityKey(_ecdsa);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.EcdsaSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
