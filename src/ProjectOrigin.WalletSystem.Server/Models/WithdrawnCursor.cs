using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record WithdrawnCursor
{
    public required string StampName { get; set; }
    public required long SyncPosition { get; set; }
    public required DateTimeOffset LastSyncDate { get; set; }
}
