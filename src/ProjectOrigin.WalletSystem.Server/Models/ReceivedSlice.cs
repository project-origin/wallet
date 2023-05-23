using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record ReceivedSlice(Guid Id,
                    Guid WalletSectionId,
                    int WalletSectionPosition,
                    string Registry,
                    Guid CertificateId,
                    long Quantity,
                    byte[] RandomR);
