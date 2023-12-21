using System;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.WalletSystem.Server.Extensions;

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
}
