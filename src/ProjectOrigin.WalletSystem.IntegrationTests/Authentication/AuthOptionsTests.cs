using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using ProjectOrigin.WalletSystem.Server.Options;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.Authentication;

public class AuthOptionsTests
{
    [Fact]
    public void InvalidAuthType()
    {
        var options = new AuthOptions
        {
            Type = (AuthType)999
        };

        var results = options.Validate(new ValidationContext(options));
        results.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Not supported authentication type: ”999”");
    }

    [Fact]
    public void MissingHeader()
    {
        var options = new AuthOptions
        {
            Type = AuthType.Header,
        };

        var results = options.Validate(new ValidationContext(options));
        results.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Header options are required for Header authentication");
    }

    [Fact]
    public void Invalid_HeaderName()
    {
        var options = new AuthOptions
        {
            Type = AuthType.Header,
            Header = new HeaderOptions()
            {
                HeaderName = ""
            }
        };

        var results = options.Validate(new ValidationContext(options));
        results.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("The HeaderName field is required.");
    }


    [Fact]
    public void Valid_HeaderAuth()
    {
        var options = new AuthOptions
        {
            Type = AuthType.Header,
            Header = new HeaderOptions
            {
                HeaderName = "MyName"
            }
        };

        var results = options.Validate(new ValidationContext(options));
        results.Should().BeEmpty();
    }

    [Fact]
    public void Invalid_MissingJwtConfig()
    {
        var options = new AuthOptions
        {
            Type = AuthType.Jwt,
        };

        var results = options.Validate(new ValidationContext(options));
        results.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Jwt options are required for Jwt authentication");
    }

    [Fact]
    public void Valid_JwtConfig()
    {
        var options = new AuthOptions
        {
            Type = AuthType.Jwt,
            Jwt = new JwtOptions()
        };

        var results = options.Validate(new ValidationContext(options));
        results.Should().BeEmpty();
    }
}
