using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
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
        return await _connection.ExecuteScalarAsync<int>("SELECT MAX(WalletPosition) FROM DepositEndpoints WHERE WalletId = @id", new { id }) + 1;
    }

    public Task CreateDepositEndpoint(DepositEndpoint depositEndpoint)
    {
        return _connection.ExecuteAsync(@"INSERT INTO DepositEndpoints(Id, WalletId, WalletPosition, PublicKey, Owner, ReferenceText, Endpoint) VALUES (@id, @walletId, @walletPosition, @publicKey, @owner, @referenceText, @endpoint)", new { depositEndpoint.Id, depositEndpoint.WalletId, depositEndpoint.WalletPosition, depositEndpoint.PublicKey, depositEndpoint.Owner, depositEndpoint.ReferenceText, depositEndpoint.Endpoint });
    }

    public async Task<DepositEndpoint?> GetDepositEndpointFromPublicKey(IHDPublicKey publicKey)
    {
        var publicKeyBytes = publicKey.Export().ToArray();
        return await _connection.QuerySingleOrDefaultAsync<DepositEndpoint>("SELECT * FROM DepositEndpoints WHERE PublicKey = @publicKeyBytes", new { publicKeyBytes });
    }

    public async Task<DepositEndpoint?> GetReceiverDepositEndpoint(Guid depositEndpointId)
    {
        return await _connection.QuerySingleOrDefaultAsync<DepositEndpoint?>("SELECT * FROM DepositEndpoints WHERE Id = @depositEndpointId", new { depositEndpointId });
    }
}
