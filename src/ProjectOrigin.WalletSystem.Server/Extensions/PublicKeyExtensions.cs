using System;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.WalletSystem.Server.Extensions;

public static class PublicKeyExtensions
{
    public static IPublicKey ImportKey(this Electricity.V1.PublicKey key)
    {
        switch (key.Type)
        {
            case Electricity.V1.KeyType.Secp256K1:
                return new Secp256k1Algorithm().ImportPublicKey(key.Content.Span);

            case Electricity.V1.KeyType.Ed25519:
                return new Ed25519Algorithm().ImportPublicKey(key.Content.Span);

            default:
                throw new NotSupportedException();
        }
    }
}
