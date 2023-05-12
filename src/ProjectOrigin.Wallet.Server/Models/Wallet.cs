using System;
using ProjectOrigin.Wallet.Server.HDWallet;

namespace ProjectOrigin.Wallet.Server.Models;

public record WalletA(Guid Id, string Owner, IHDPrivateKey PrivateKey);
