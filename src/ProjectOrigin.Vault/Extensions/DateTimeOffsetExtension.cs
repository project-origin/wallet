using System;

namespace ProjectOrigin.Vault.Extensions
{
    public static class DateTimeOffsetExtension
    {
        public static DateTimeOffset ToUtcTime(this DateTimeOffset date)
        {
            return DateTimeOffset.FromUnixTimeSeconds(date.ToUnixTimeSeconds());
        }
    }
}
