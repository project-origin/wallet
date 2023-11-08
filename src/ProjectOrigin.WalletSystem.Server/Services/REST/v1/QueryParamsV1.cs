using System.Text.Json.Serialization;

namespace ProjectOrigin.WalletSystem.Server.Services.REST.v1;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TimeAggregate
{
    None,
    Hour,
    Day,
    Week,
    Month,
    Year
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SliceState
{
    Available,
    Total
}
