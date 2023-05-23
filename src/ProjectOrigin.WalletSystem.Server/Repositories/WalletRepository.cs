using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.WalletSystem.Server.HDWallet;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public class WalletRepository
{
    private IDbConnection _connection;

    public WalletRepository(IDbConnection connection)
    {
        this._connection = connection;
    }

    public Task<int> Create(Wallet wallet)
    {
        return _connection.ExecuteAsync(@"INSERT INTO Wallets(Id, Owner, PrivateKey) VALUES (@id, @owner, @privateKey)", new { wallet.Id, wallet.Owner, wallet.PrivateKey });
    }

    public Task<Wallet?> GetWalletByOwner(string owner)
    {
        return _connection.QuerySingleOrDefaultAsync<Wallet?>("SELECT * FROM Wallets WHERE Owner = @owner", new { owner });
    }

    public async Task<int> GetNextWalletPosition(Guid id)
    {
        return await _connection.ExecuteScalarAsync<int>("SELECT MAX(WalletPosition) FROM WalletSections WHERE WalletId = @id", new { id }) + 1;
    }

    public Task CreateSection(WalletSection section)
    {
        return _connection.ExecuteAsync(@"INSERT INTO WalletSections(Id, WalletId, WalletPosition, PublicKey) VALUES (@id, @walletId, @walletPosition, @publicKey)", new { section.Id, section.WalletId, section.WalletPosition, section.PublicKey });
    }

    public async Task<WalletSection?> GetWalletSectionFromPublicKey(IHDPublicKey publicKey)
    {
        var publicKeyBytes = publicKey.Export().ToArray();
        return await _connection.QuerySingleOrDefaultAsync<WalletSection>("SELECT * FROM WalletSections WHERE PublicKey = @publicKeyBytes", new { publicKeyBytes });
    }
}
