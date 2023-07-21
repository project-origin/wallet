using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record Slice(Guid Id,
                    Guid DepositEndpointId,
                    int DepositEndpointPosition,
                    Guid RegistryId,
                    Guid CertificateId,
                    long Quantity,
                    byte[] RandomR);
