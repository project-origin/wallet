using System;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.WalletSystem.Server.Extensions;

public static class PublicKeyExtensions
{
    public static IPublicKey ToModel(this PublicKey key)
    {
        switch (key.Type)
        {
            case KeyType.Secp256K1:
                return (new Secp256k1Algorithm()).ImportPublicKey(key.Content.Span);

            case KeyType.Ed25519:
                return (new Ed25519Algorithm()).ImportPublicKey(key.Content.Span);

            default:
                throw new NotSupportedException();
        }
    }
}
