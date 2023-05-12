using System;
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

    public async Task<int> Create(WalletA wallet)
    {
        return await _connection.ExecuteAsync(@"INSERT INTO Wallets(Id, Owner, PrivateKey) VALUES (@id, @owner, @privateKey)", new { wallet.Id, wallet.Owner, wallet.PrivateKey });
    }

    public async Task<WalletA?> GetWallet(string owner)
    {
        return await _connection.QuerySingleOrDefaultAsync<WalletA>("SELECT * FROM Wallets WHERE Owner = @owner", new { owner });
    }

    public async Task<int> GetNextWalletPosition(Guid id)
    {
        return await _connection.ExecuteScalarAsync<int>("SELECT MAX(WalletPosition) FROM WalletSections WHERE WalletId = @id", new { id }) + 1;
    }

    public async Task CreateSection(WalletSection section)
    {
        await _connection.ExecuteAsync(@"INSERT INTO WalletSections(Id, WalletId, WalletPosition, PublicKey) VALUES (@id, @walletId, @walletPosition, @publicKey)", new { section.Id, section.WalletId, section.WalletPosition, section.PublicKey });
    }
}
