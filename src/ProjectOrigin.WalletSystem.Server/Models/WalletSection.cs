using System;
using ProjectOrigin.WalletSystem.Server.HDWallet;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record WalletSection(Guid Id, Guid WalletId, int WalletPosition, IHDPublicKey PublicKey);
