using System;
using ProjectOrigin.WalletSystem.Server.HDWallet;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record OwnerWallet(Guid Id, string Owner, IHDPrivateKey PrivateKey);
