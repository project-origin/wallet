using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public class RegistryRepository
{
    private readonly IDbConnection _connection;

    public RegistryRepository(IDbConnection connection)
    {
        this._connection = connection;
    }

    public Task<RegistryModel> GetRegistryFromId(Guid registryId)
    {
        return _connection.QuerySingleAsync<RegistryModel>("SELECT * FROM Registries WHERE Id = @registryId", new { registryId });
    }

    public Task<RegistryModel?> GetRegistryFromName(string registry)
    {
        return _connection.QuerySingleOrDefaultAsync<RegistryModel?>("SELECT * FROM Registries WHERE Name = @registry", new { registry });
    }

    public Task InsertRegistry(RegistryModel registry)
    {
        return _connection.ExecuteAsync(@"INSERT INTO Registries(Id, Name) VALUES (@id, @name)", new { registry.Id, registry.Name });
    }
}
