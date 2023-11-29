using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using AutoFixture;
using Grpc.Core;
using Microsoft.IdentityModel.Tokens;

namespace ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;

public class JwtTokenIssuerFixture : IDisposable
{
    public string Issuer { get; init; } = "TestIssuer";
    public string Audience { get; } = "WalletSystem";
    public int ExpirationMinutes { get; } = 60;
    public string PemFilepath { get; }

    private readonly ECDsa _ecdsa;
    private bool _disposed = false;

    public JwtTokenIssuerFixture()
    {
        _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        PemFilepath = Path.GetTempFileName();
        var pem = _ecdsa.ExportSubjectPublicKeyInfoPem();
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

        var key = new ECDsaSecurityKey(_ecdsa);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.EcdsaSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
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

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                File.Delete(PemFilepath);
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~JwtTokenIssuerFixture()
    {
        Dispose(false);
    }

}
