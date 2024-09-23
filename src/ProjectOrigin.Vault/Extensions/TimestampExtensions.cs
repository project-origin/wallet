using System;
using Google.Protobuf.WellKnownTypes;

namespace ProjectOrigin.Vault.Extensions;

public static class TimestampExtensions
{
    public static DateTimeOffset? ToNullableDateTimeOffset(this Timestamp date)
    {
        return date == new Timestamp() ? date.ToDateTime() : null;
    }
}
