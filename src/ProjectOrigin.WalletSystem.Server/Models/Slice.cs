using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public enum SliceState
{
    NotSliced = 1,
    Slicing = 2,
    Sliced = 3
}

public record Slice(Guid Id,
                    Guid DepositEndpointId,
                    int DepositEndpointPosition,
                    Guid RegistryId,
                    Guid CertificateId,
                    long Quantity,
                    byte[] RandomR,
                    SliceState SliceState);
