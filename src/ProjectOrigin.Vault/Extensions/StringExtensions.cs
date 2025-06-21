using System;
using System.Linq;

namespace ProjectOrigin.Vault.Extensions;

public static class StringExtensions
{
    public static bool TryParseTimeZone(this string timeZone, out TimeZoneInfo timeZoneInfo)
    {
        try
        {
            timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            return true;
        }
        catch
        {
            timeZoneInfo = null!;
            return false;
        }
    }

    public static string CombineUrl(params string[] segments)
    {
        return string.Join('/', segments
            .Select(s => s.Trim('/'))
            .Where(s => s.Length > 0));
    }
}
