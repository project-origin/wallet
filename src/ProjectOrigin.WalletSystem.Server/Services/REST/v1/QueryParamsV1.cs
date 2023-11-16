using System.Text.Json.Serialization;

namespace ProjectOrigin.WalletSystem.Server.Services.REST.v1;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TimeAggregate
{
    Actual = 0,
    Total = 1,
    Year = 2,
    Month = 3,
    Week = 4,
    Day = 5,
    Hour = 6,
    QuarterHour = 7,
}
