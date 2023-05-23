using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record Slice(Guid Id, Guid WalletSectionId, int WalletSectionPosition, Guid RegistryId, Guid CertificateId, long Quantity, byte[] RandomR, SliceState State);
