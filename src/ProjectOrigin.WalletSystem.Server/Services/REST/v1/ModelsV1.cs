using System.Collections.Generic;
using System;

namespace ProjectOrigin.WalletSystem.Server.Services.REST.v1;

public record ResultList<T>
{
    public required IEnumerable<T> Result { get; init; }
}

public enum CertificateType
{
    Consumption = 1,
    Production = 2
}

public record FederatedStreamId
{
    public required string Registry { get; init; }
    public required Guid StreamId { get; init; }
}

public record GranularCertificate
{
    public required FederatedStreamId FederatedStreamId { get; init; }
    public required uint Quantity { get; init; }
    public required long Start { get; init; }
    public required long End { get; init; }
    public required string GridArea { get; init; }
    public required CertificateType CertificateType { get; init; }
    public required Dictionary<string, string> Attributes { get; init; }
}
public record Transfer
{
    public required FederatedStreamId FederatedStreamId { get; init; }
    public required string ReceiverId { get; init; }
    public required long Quantity { get; init; }
    public required long Start { get; init; }
    public required long End { get; init; }
    public required string GridArea { get; init; }
}

public record AggregatedCertificates
{
    public required long Start { get; init; }
    public required long End { get; init; }
    public required long Quantity { get; init; }
    public required CertificateType Type { get; init; }
}
public record AggregatedTransfers
{
    public required long Start { get; init; }
    public required long End { get; init; }
    public required long Quantity { get; init; }
}
