using System;

namespace ProjectOrigin.Wallet.Server.Models;

public record Certificate(Guid Id, Guid RegistryId, bool Loaded);
