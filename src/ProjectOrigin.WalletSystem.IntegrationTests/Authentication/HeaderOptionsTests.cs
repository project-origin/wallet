using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using ProjectOrigin.WalletSystem.Server.Options;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.Authentication;

public class HeaderOptionsTests
{

    [Fact]
    public void HeaderName_Required()
    {
        var options = new HeaderOptions
        {
            Name = ""
        };

        var results = options.Validate(new ValidationContext(options));
        results.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Name is required");
    }

}
