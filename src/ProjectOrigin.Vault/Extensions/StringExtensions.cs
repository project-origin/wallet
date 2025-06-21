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
        return string.Join('/',
            segments
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Replace('\\', '/').Trim('/'))
        );
    }
}
