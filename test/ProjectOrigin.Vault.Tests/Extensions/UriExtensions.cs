using System;
using ProjectOrigin.Vault.Extensions;
using Xunit;

namespace ProjectOrigin.Vault.Tests.Extensions;

public class UriExtensions
{
    [Fact]
    public void Combine_WithBaseUriAndSinglePath_ReturnsCombinedUri()
    {
        var baseUri = new Uri("http://example.com");
        var result = baseUri.Combine("path");
        Assert.Equal(new Uri("http://example.com/path"), result);
    }

    [Fact]
    public void Combine_WithBaseUriAndMultiplePaths_ReturnsCombinedUri()
    {
        var baseUri = new Uri("http://example.com");
        var result = baseUri.Combine("path1", "path2");
        Assert.Equal(new Uri("http://example.com/path1/path2"), result);
    }

    [Fact]
    public void Combine_WithBaseUriAndEmptyPaths_ReturnsBaseUri()
    {
        var baseUri = new Uri("http://example.com");
        var result = baseUri.Combine();
        Assert.Equal(baseUri, result);
    }

    [Fact]
    public void Combine_WithBaseUriAndPathsContainingSlashes_ReturnsCombinedUri()
    {
        var baseUri = new Uri("http://example.com");
        var result = baseUri.Combine("/path1/", "/path2/");
        Assert.Equal(new Uri("http://example.com/path1/path2"), result);
    }

    [Fact]
    public void Combine_WithNullBaseUri_ThrowsArgumentNullException()
    {
        Uri baseUri = null!;
        Assert.Throws<ArgumentNullException>(() => baseUri.Combine("path"));
    }
}
