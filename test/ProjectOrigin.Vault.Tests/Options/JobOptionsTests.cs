using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using ProjectOrigin.Vault.Options;
using Xunit;

namespace ProjectOrigin.Vault.Tests.Options;

public class JobOptionsTests
{
    [Theory]
    [InlineData(-900)]
    [InlineData(-1)]
    [InlineData(0)]
    public void Validate_Error(int init)
    {
        var jobOptions = new JobOptions
        {
            CheckForWithdrawnCertificatesIntervalInSeconds = init,
            ExpireCertificatesIntervalInSeconds = init
        };

        var result = jobOptions.Validate(new ValidationContext(jobOptions));

        result.Should().Contain(x =>
            x.ErrorMessage == "CheckForWithdrawnCertificatesIntervalInSeconds must be greater than 0" ||
            x.ErrorMessage == "ExpireCertificatesIntervalInSeconds must be greater than 0");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(900)]
    public void Validate(int init)
    {
        var jobOptions = new JobOptions
        {
            CheckForWithdrawnCertificatesIntervalInSeconds = init,
            ExpireCertificatesIntervalInSeconds = init
        };

        var result = jobOptions.Validate(new ValidationContext(jobOptions));

        result.Should().BeEmpty();
    }
}
