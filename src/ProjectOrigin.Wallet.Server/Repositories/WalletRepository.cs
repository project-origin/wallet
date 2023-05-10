using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.Wallet.Server.Models;

namespace ProjectOrigin.Wallet.Server.Repositories;

public class WalletRepository
{
    private IDbTransaction _transaction;

    private IDbConnection _connection => _transaction.Connection ?? throw new InvalidOperationException("No connection.");

    public WalletRepository(IDbTransaction transaction)
    {
        this._transaction = transaction;
    }

    public async Task<int> Create(MyTable table)
    {
        var result = await _connection.ExecuteAsync(@"INSERT INTO MyTable(Foo) VALUES (@foo)", table);

        return result;
    }

    public async Task<IEnumerable<MyTable>> GetAll()
    {
        var result = await _connection.QueryAsync<MyTable>("SELECT * FROM MyTable");

        return result;
    }
}
