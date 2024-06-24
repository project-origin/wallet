using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public enum TransferStatusState
{
    Pending,
    Completed,
    Failed
}

public record TransferStatus
{
    public required Guid TransferRequestId { get; init; }
    public required TransferStatusState Status { get; init; }
}
