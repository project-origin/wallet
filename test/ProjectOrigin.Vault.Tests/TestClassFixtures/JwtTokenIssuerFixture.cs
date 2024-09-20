using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using AutoFixture;
using Microsoft.IdentityModel.Tokens;

namespace ProjectOrigin.Vault.Tests.TestClassFixtures;

public sealed class JwtTokenIssuerFixture : IDisposable
{
    private readonly RSA _rsa = RSA.Create();

    public string KeyType => "RSA";
    public string Algorithm => SecurityAlgorithms.RsaSha256;
    public byte[] PublicKeyInfo => _rsa.ExportSubjectPublicKeyInfo();
    public int ExpirationMinutes => 60;
    public string PemFilepath { get; private init; }
    public string Audience => "WalletSystem";

    public string Issuer { get; init; } = "TestIssuer";

    public JwtTokenIssuerFixture()
    {
        PemFilepath = Path.GetTempFileName();
        var pem = _rsa.ExportSubjectPublicKeyInfoPem();
        File.WriteAllText(PemFilepath, pem);
    }

    public string GenerateRandomToken()
    {
        var fixture = new Fixture();
        var subject = fixture.Create<string>();
        var name = fixture.Create<string>();
        return GenerateToken(subject, name);
    }

    public string GenerateToken(string subject, string name, string[]? scopes = null)
    {
        var claims = new[]
        {
            new Claim("sub", subject),
            new Claim("name", name),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim("scope", string.Join(" ", scopes ?? Array.Empty<string>())),
        };

        var key = new RsaSecurityKey(_rsa);
        var credentials = new SigningCredentials(key, Algorithm);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            //notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(ExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GetJsonOpenIdConfiguration()
    {
        return JsonSerializer.Serialize(new
        {
            issuer = $"{Issuer}",
            jwks_uri = $"{Issuer}/keys",
        });
    }

    public string GetJsonKeys()
    {
        return JsonSerializer.Serialize(new
        {
            keys = new[]{
                new
                    {
                        use = "sig",
                        kty = KeyType,
                        alg = Algorithm,
                        n = Convert.ToBase64String(_rsa.ExportParameters(true).Modulus!),
                        e = Convert.ToBase64String(_rsa.ExportParameters(true).Exponent!),
                    }
            }
        });
    }

    public void Dispose()
    {
        File.Delete(PemFilepath);
    }
}
