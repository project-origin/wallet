using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using ProjectOrigin.Vault.Options;
using Xunit;

namespace ProjectOrigin.Vault.Tests.Authentication;

public class HeaderOptionsTests
{

    [Fact]
    public void HeaderName_Required()
    {
        var options = new HeaderOptions
        {
            HeaderName = ""
        };

        var results = options.Validate(new ValidationContext(options));
        results.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("HeaderName is required");
    }

}
