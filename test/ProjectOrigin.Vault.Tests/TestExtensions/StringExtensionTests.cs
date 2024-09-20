using FluentAssertions;
using Xunit;

namespace ProjectOrigin.Vault.Extensions;

public class StringExtensionTests
{
    [Fact]
    public void InvalidTimeZone()
    {
        // Arrange
        var timeZone = "InvalidTimeZone";

        // Act
        var result = timeZone.TryParseTimeZone(out var timeZoneInfo);

        // Assert
        result.Should().BeFalse();
        timeZoneInfo.Should().BeNull();
    }

    [Theory]
    [InlineData("Europe/Copenhagen")]
    [InlineData("Europe/London")]
    [InlineData("America/Toronto")]
    public void ValidTimeZone(string timeZone)
    {
        // Act
        var result = timeZone.TryParseTimeZone(out var timeZoneInfo);

        // Assert
        result.Should().BeTrue();
        timeZoneInfo.Should().NotBeNull();
    }
}
