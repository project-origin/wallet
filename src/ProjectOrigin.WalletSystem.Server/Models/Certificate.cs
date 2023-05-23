using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record Certificate(Guid Id, Guid RegistryId, CertificateState State);
