using System;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.Vault.Extensions;

public static class IHDAlgorithmExtensions
{
    public static bool TryImportHDPrivateKey(this IHDAlgorithm hdAlgorithm, byte[] privateKeyBytes, out IHDPrivateKey hdPrivateKey)
    {
        try
        {
            hdPrivateKey = hdAlgorithm.ImportHDPrivateKey(privateKeyBytes);
            return true;
        }
        catch (Exception)
        {
            hdPrivateKey = null!;
            return false;
        }
    }

    public static bool TryImportHDPublicKey(this IHDAlgorithm hdAlgorithm, byte[] publicKeyBytes, out IHDPublicKey hdPublicKey)
    {
        try
        {
            hdPublicKey = hdAlgorithm.ImportHDPublicKey(publicKeyBytes);
            return true;
        }
        catch (Exception)
        {
            hdPublicKey = null!;
            return false;
        }
    }
}
