using System;
using ProjectOrigin.Wallet.Server.HDWallet;

namespace ProjectOrigin.Wallet.Server.Models;

public record WalletSection(Guid Id, Guid WalletId, int WalletPosition, IHDPublicKey PublicKey);
