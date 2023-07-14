using System;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record DepositEndpoint(Guid Id, Guid? WalletId, int? WalletPosition, IHDPublicKey PublicKey, string Owner, string ReferenceText);
