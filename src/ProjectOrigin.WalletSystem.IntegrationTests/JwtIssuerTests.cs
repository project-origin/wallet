using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FluentAssertions;
using ProjectOrigin.WalletSystem.Server.Options;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests;

public class JwtIssuerTests
{
    [Fact]
    public void ECDsaSuccess()
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pemFilepath = Path.GetTempFileName();
        var pem = ecdsa.ExportSubjectPublicKeyInfoPem();
        File.WriteAllText(pemFilepath, pem);

        var issuer = new JwtIssuer
        {
            Type = "ecdsa",
            PemKeyFile = pemFilepath
        };

        var results = issuer.Validate(new ValidationContext(issuer));
        results.Any(x => x != ValidationResult.Success).Should().BeFalse();
    }

    [Fact]
    public void RsaSuccess()
    {
        var rsa = RSA.Create();
        var pemFilepath = Path.GetTempFileName();
        var pem = rsa.ExportSubjectPublicKeyInfoPem();
        File.WriteAllText(pemFilepath, pem);

        var issuer = new JwtIssuer
        {
            Type = "rsa",
            PemKeyFile = pemFilepath
        };

        var results = issuer.Validate(new ValidationContext(issuer));
        results.Any(x => x != ValidationResult.Success).Should().BeFalse();
    }

    [Fact]
    public void FileNotFound()
    {
        var pemFilepath = Path.GetTempFileName();

        var issuer = new JwtIssuer
        {
            Type = "rsa",
            PemKeyFile = "/hello.pem"
        };

        var results = issuer.Validate(new ValidationContext(issuer));
        results.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Issuer key file ”/hello.pem” not found");
    }

    [Fact]
    public void InvalidType()
    {
        var rsa = RSA.Create();
        var pemFilepath = Path.GetTempFileName();
        var pem = rsa.ExportSubjectPublicKeyInfoPem();
        File.WriteAllText(pemFilepath, pem);

        var issuer = new JwtIssuer
        {
            Type = "hello",
            PemKeyFile = pemFilepath
        };

        var results = issuer.Validate(new ValidationContext(issuer));
        results.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Issuer key could not be imported as type ”hello”, Issuer key type ”hello” not implemeted");
    }
}
