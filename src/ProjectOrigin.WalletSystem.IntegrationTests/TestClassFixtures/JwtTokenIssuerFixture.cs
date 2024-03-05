using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using AutoFixture;
using Grpc.Core;
using Microsoft.IdentityModel.Tokens;

namespace ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;

public sealed class JwtTokenIssuerFixture : IDisposable
{
    private readonly RSA _rsa = RSA.Create();
    public string KeyType => "RSA";
    public string Issuer { get; init; } = "TestIssuer";
    public string Audience => "WalletSystem";
    public string Algorithm => SecurityAlgorithms.RsaSha256;
    public byte[] PublicKeyInfo => _rsa.ExportSubjectPublicKeyInfo();
    public int ExpirationMinutes => 60;

    public string PemFilepath { get; }

    public JwtTokenIssuerFixture()
    {
        PemFilepath = Path.GetTempFileName();
        var pem = _rsa.ExportSubjectPublicKeyInfoPem();
        File.WriteAllText(PemFilepath, pem);
    }

    public string GenerateToken(string subject, string name)
    {
        var claims = new[]
        {
            new Claim("sub", subject),
            new Claim("name", name),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
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

    public (string, Metadata) GenerateUserHeader()
    {
        var fixture = new Fixture();
        var subject = fixture.Create<string>();
        var name = fixture.Create<string>();

        var token = GenerateToken(subject, name);

        var headers = new Metadata
        {
            { "Authorization", $"Bearer {token}" }
        };

        return (subject, headers);
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

    public string GetJsonOpenIdConfiguration()
    {
        return JsonSerializer.Serialize(new
        {
            issuer = $"{Issuer}",
            authorization_endpoint = $"{Issuer}/auth",
            token_endpoint = $"{Issuer}/token",
            jwks_uri = $"{Issuer}/keys",
            userinfo_endpoint = $"{Issuer}/userinfo",
            device_authorization_endpoint = $"{Issuer}/device/code",
            grant_types_supported = new[]{
                "authorization_code",
                "refresh_token",
                "urn:ietf:params:oauth:grant-type:device_code",
                "urn:ietf:params:oauth:grant-type:token-exchange"
            },
            response_types_supported = new[]{
                "code"
            },
            subject_types_supported = new[]{
                "public"
            },
            id_token_signing_alg_values_supported = new[]{
                Algorithm
            },
            code_challenge_methods_supported = new[]{
                "S256",
                "plain"
            },
            scopes_supported = new[]{
                "openid",
                "email",
                "groups",
                "profile",
                "offline_access"
            },
            token_endpoint_auth_methods_supported = new[]{
                "client_secret_basic",
                "client_secret_post"
            },
            claims_supported = new[]{
                "iss",
                "sub",
                "aud",
                "iat",
                "exp",
                "email",
                "email_verified",
                "locale",
                "name",
                "preferred_username",
                "at_hash"
            },
        });
    }

    public void Dispose()
    {
        File.Delete(PemFilepath);
    }
}
