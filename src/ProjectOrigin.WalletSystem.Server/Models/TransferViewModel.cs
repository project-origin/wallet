using System;
using System.Collections.Generic;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record TransferViewModel
{
    public required Guid CertificateId { get; init; }
    public required string RegistryName { get; init; }
    public required Guid ReceiverId { get; init; }
    public required string GridArea { get; init; }
    public required long Quantity { get; init; }
    public required DateTimeOffset StartDate { get; init; }
    public required DateTimeOffset EndDate { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public List<CertificateAttribute> Attributes { get; } = new();
}
