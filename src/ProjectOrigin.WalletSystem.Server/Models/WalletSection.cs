using System;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record WalletSection(Guid Id, Guid WalletId, int WalletPosition, IHDPublicKey PublicKey);
