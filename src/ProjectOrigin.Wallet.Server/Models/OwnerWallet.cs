using System;
using ProjectOrigin.Wallet.Server.HDWallet;

namespace ProjectOrigin.Wallet.Server.Models;

public record OwnerWallet(Guid Id, string Owner, IHDPrivateKey PrivateKey);
