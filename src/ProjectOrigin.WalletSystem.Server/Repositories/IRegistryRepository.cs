using System;
using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public interface IRegistryRepository
{
    Task<RegistryModel> GetRegistryFromId(Guid registryId);
    Task<RegistryModel?> GetRegistryFromName(string registry);
    Task InsertRegistry(RegistryModel registry);
}
