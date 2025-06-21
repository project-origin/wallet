using System;
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

    [Fact]
    public void CombineUrl_ReturnsCombinedUrl_WhenSegmentsAreValid()
    {
        var segments = new[] { "http://example.com", "/api/", "/v1/resource/" };

        var result = StringExtensions.CombineUrl(segments);

        result.Should().Be("http://example.com/api/v1/resource");
    }

    [Theory]
    [InlineData(new[] { "foo", "bar" }, "foo/bar")]
    [InlineData(new[] { "foo/", "/bar/", "/baz" }, "foo/bar/baz")]
    [InlineData(new[] { "/foo", "bar/", "baz/" }, "foo/bar/baz")]
    [InlineData(new[] { "", "foo", "", "bar" }, "foo/bar")]
    [InlineData(new[] { "foo" }, "foo")]
    [InlineData(new[] { "" }, "")]
    public void CombineUrl_TrimsAndJoins(string[] segments, string expected)
    {
        var actual = StringExtensions.CombineUrl(segments);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CombineUrl_WithNoArguments_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, StringExtensions.CombineUrl());
    }

    [Fact]
    public void CombineUrl_WithNullArray_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => StringExtensions.CombineUrl(null!));
    }
}
