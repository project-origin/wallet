using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.Wallet.Server.HDWallet;
using ProjectOrigin.Wallet.Server.Models;

namespace ProjectOrigin.Wallet.Server.Repositories;

public class WalletRepository
{
    private IDbConnection _connection;

    public WalletRepository(IDbConnection connection)
    {
        this._connection = connection;
    }

    public Task<int> Create(OwnerWallet wallet)
    {
        return _connection.ExecuteAsync(@"INSERT INTO Wallets(Id, Owner, PrivateKey) VALUES (@id, @owner, @privateKey)", new { wallet.Id, wallet.Owner, wallet.PrivateKey });
    }

    public Task<OwnerWallet?> GetWallet(string owner)
    {
        return _connection.QuerySingleOrDefaultAsync<OwnerWallet?>("SELECT * FROM Wallets WHERE Owner = @owner", new { owner });
    }

    public async Task<int> GetNextWalletPosition(Guid id)
    {
        return await _connection.ExecuteScalarAsync<int>("SELECT MAX(WalletPosition) FROM WalletSections WHERE WalletId = @id", new { id }) + 1;
    }

    public Task CreateSection(WalletSection section)
    {
        return _connection.ExecuteAsync(@"INSERT INTO WalletSections(Id, WalletId, WalletPosition, PublicKey) VALUES (@id, @walletId, @walletPosition, @publicKey)", new { section.Id, section.WalletId, section.WalletPosition, section.PublicKey });
    }
}
