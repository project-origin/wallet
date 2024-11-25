using System.ComponentModel.DataAnnotations;
using System.Linq;
using FluentAssertions;
using ProjectOrigin.Vault.Options;
using Xunit;

namespace ProjectOrigin.Vault.Tests.Options;

public class JobOptionsTests
{
    [Theory]
    [InlineData(3, 2)]
    [InlineData(60, 40)]
    [InlineData(900, 600)] // 15 minutes
    public void TimeBeforeItIsOkToRunCheckForWithdrawnCertificatesAgain_ReturnsExpectedValue(int init, int expected)
    {
        var jobOptions = new JobOptions
        {
            CheckForWithdrawnCertificatesIntervalInSeconds = init
        };

        var result = jobOptions.TimeBeforeItIsOkToRunCheckForWithdrawnCertificatesAgain();

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(-900)]
    [InlineData(-1)]
    [InlineData(0)]
    public void Validate_Error(int init)
    {
        var jobOptions = new JobOptions
        {
            CheckForWithdrawnCertificatesIntervalInSeconds = init
        };

        var result = jobOptions.Validate(new ValidationContext(jobOptions));

        result.Should().ContainSingle();
        result.First().ErrorMessage.Should().Be("CheckForWithdrawnCertificatesIntervalInSeconds must be greater than 0");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(900)]
    public void Validate(int init)
    {
        var jobOptions = new JobOptions
        {
            CheckForWithdrawnCertificatesIntervalInSeconds = init
        };

        var result = jobOptions.Validate(new ValidationContext(jobOptions));

        result.Should().BeEmpty();
    }
}
