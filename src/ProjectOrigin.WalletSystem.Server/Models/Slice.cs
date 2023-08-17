using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public enum SliceState
{
    Available = 1,
    Slicing = 2, // Reserved
    Registering = 3,
    Sliced = 4,
    Transferred = 5,
}

public record Slice(Guid Id,
                    Guid DepositEndpointId,
                    int DepositEndpointPosition,
                    Guid RegistryId,
                    Guid CertificateId,
                    long Quantity,
                    byte[] RandomR,
                    SliceState SliceState);
