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

public record ClaimedCertificate
{
    public required FederatedStreamId FederatedStreamId { get; init; }
    public required long Start { get; init; }
    public required long End { get; init; }
    public required string GridArea { get; init; }
    public required Dictionary<string, string> Attributes { get; init; }
}

public record Claim
{
    public required Guid ClaimId { get; init; }
    public required uint Quantity { get; init; }
    public required ClaimedCertificate ProductionCertificate { get; init; }
    public required ClaimedCertificate ConsumptionCertificate { get; init; }
}

/// <summary>
/// A request to transfer a certificate to another wallet.
/// </summary>
public record TransferRequest
{
    /// <summary>
    /// The federated stream id of the certificate to transfer.
    /// </summary>
    public required FederatedStreamId CertificateId { get; init; }

    /// <summary>
    /// The id of the wallet to transfer the certificate to.
    /// </summary>
    public required Guid ReceiverId { get; init; }

    /// <summary>
    /// The quantity of the certificate to transfer.
    /// </summary>
    public required uint Quantity { get; init; }

    /// <summary>
    /// List of hashed attributes to transfer with the certificate.
    /// </summary>
    public required string[] HashedAttributes { get; init; }
}

/// <summary>
/// A response to a transfer request.
/// </summary>
public record TransferResponse
{
    /// <summary>
    /// The id of the transfer request.
    /// </summary>
    public required Guid TransferRequestId { get; init; }
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

public record AggregatedClaims
{
    public required long Start { get; init; }
    public required long End { get; init; }
    public required long Quantity { get; init; }
}

public record AggregatedTransfers
{
    public required long Start { get; init; }
    public required long End { get; init; }
    public required long Quantity { get; init; }
}
