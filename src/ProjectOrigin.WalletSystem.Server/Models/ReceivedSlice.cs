using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record ReceivedSlice(Guid Id,
                    Guid DepositEndpointId,
                    int DepositEndpointPosition,
                    string Registry,
                    Guid CertificateId,
                    long Quantity,
                    byte[] RandomR);
