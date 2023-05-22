using System.Data;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.Wallet.Server.Models;

namespace ProjectOrigin.Wallet.Server.Repositories;

public class WalletRepository
{
    private IDbConnection _connection;

    public WalletRepository(IDbConnection connection)
    {
        this._connection = connection;
    }

    public async Task<int> Create(MyTable table)
    {
        var result = await _connection.ExecuteAsync(@"INSERT INTO MyTable(Foo) VALUES (@foo)", table);

        return result;
    }

    public async Task<MyTable?> Get(int id)
    {
        return await _connection.QuerySingleOrDefaultAsync<MyTable>("SELECT * FROM MyTable where id = @id", new { id = id });
    }
}
