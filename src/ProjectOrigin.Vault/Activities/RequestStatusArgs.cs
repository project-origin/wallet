using System;
using ProjectOrigin.Vault.Models;

namespace ProjectOrigin.Vault.Activities;

public record RequestStatusArgs
{
    public required Guid RequestId { get; set; }
    public required string Owner { get; set; }
    public required RequestStatusType RequestStatusType { get; set; }
}
