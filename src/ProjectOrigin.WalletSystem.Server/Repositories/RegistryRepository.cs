using System.Data;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public class RegistryRepository
{
    private IDbConnection _connection;

    public RegistryRepository(IDbConnection connection)
    {
        this._connection = connection;
    }

    public Task<Registry?> GetRegistryFromName(string registry)
    {
        return _connection.QueryFirstOrDefaultAsync<Registry?>("SELECT * FROM Registries WHERE Name = @registry", new { registry });
    }

    public Task InsertRegistry(Registry registry)
    {
        return _connection.ExecuteAsync(@"INSERT INTO Registries(Id, Name) VALUES (@id, @name)", new { registry.Id, registry.Name });
    }
}
