using System;
using System.Linq;

namespace ProjectOrigin.Vault.Extensions;

public static class UriExtensions
{
    public static Uri Combine(this Uri baseUri, params string[] paths)
    {
        ArgumentNullException.ThrowIfNull(baseUri);

        var combined = string.Join("/", paths.Select(p => p.Trim('/')));

        return new Uri(baseUri, combined);
    }
}

