using System;

namespace ProjectOrigin.WalletSystem.Server.Extensions;

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
}
