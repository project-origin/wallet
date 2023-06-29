using System;
using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.Server.Projections;

namespace ProjectOrigin.WalletSystem.Server.Services;

public interface IRegistryService
{
    Task<GranularCertificate?> GetGranularCertificate(string registryName, Guid certificateId);
}
