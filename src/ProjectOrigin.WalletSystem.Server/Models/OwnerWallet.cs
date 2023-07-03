using System;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record Wallet(Guid Id, string Owner, IHDPrivateKey PrivateKey);
