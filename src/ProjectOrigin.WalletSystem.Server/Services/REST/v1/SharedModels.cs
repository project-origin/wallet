using System.Collections.Generic;
using System;
using System.Text.Json.Serialization;

namespace ProjectOrigin.WalletSystem.Server.Services.REST.v1;

public record ResultList<T>()
{
    public required IEnumerable<T> Result { get; init; }
}

public enum CertificateType
{
    Consumption = 1,
    Production = 2
}

public record FederatedStreamId()
{
    public required string Registry { get; init; }
    public required Guid StreamId { get; init; }
}

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
