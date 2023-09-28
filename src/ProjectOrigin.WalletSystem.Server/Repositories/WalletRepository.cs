using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public class WalletRepository : IWalletRepository
{
    private const string RemainderReferenceText = "RemainderSection";
    private readonly IDbConnection _connection;

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

    public async Task<DepositEndpoint> CreateDepositEndpoint(Guid walletId, string referenceText)
    {
        var position = await GetNextNumberForId(walletId);

        var wallet = await GetWallet(walletId);
        var key = wallet.PrivateKey.Derive(position).Neuter();

        var newEndpoint = new DepositEndpoint
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            WalletPosition = position,
            PublicKey = key,
            Owner = wallet.Owner,
            ReferenceText = referenceText,
            Endpoint = string.Empty
        };

        await CreateDepositEndpoint(newEndpoint);
        return newEndpoint;
    }

    public async Task<DepositEndpoint> CreateReceiverDepositEndpoint(string owner, IHDPublicKey ownerPublicKey, string referenceText, string endpoint)
    {
        var newEndpoint = new DepositEndpoint
        {
            Id = Guid.NewGuid(),
            WalletId = null,
            WalletPosition = null,
            PublicKey = ownerPublicKey,
            Owner = owner,
            ReferenceText = referenceText,
            Endpoint = endpoint
        };
        await CreateDepositEndpoint(newEndpoint);
        return newEndpoint;
    }

    private Task CreateDepositEndpoint(DepositEndpoint depositEndpoint)
    {
        return _connection.ExecuteAsync(@"INSERT INTO DepositEndpoints(Id, WalletId, WalletPosition, PublicKey, Owner, ReferenceText, Endpoint) VALUES (@id, @walletId, @walletPosition, @publicKey, @owner, @referenceText, @endpoint)", new { depositEndpoint.Id, depositEndpoint.WalletId, depositEndpoint.WalletPosition, depositEndpoint.PublicKey, depositEndpoint.Owner, depositEndpoint.ReferenceText, depositEndpoint.Endpoint });
    }

    public async Task<DepositEndpoint?> GetDepositEndpointFromPublicKey(IHDPublicKey publicKey)
    {
        var publicKeyBytes = publicKey.Export().ToArray();
        return await _connection.QuerySingleOrDefaultAsync<DepositEndpoint>("SELECT * FROM DepositEndpoints WHERE PublicKey = @publicKeyBytes and WalletId is not null", new { publicKeyBytes });
    }

    public Task<DepositEndpoint> GetDepositEndpoint(Guid depositEndpointId)
    {
        return _connection.QuerySingleAsync<DepositEndpoint>("SELECT * FROM DepositEndpoints WHERE Id = @depositEndpointId", new { depositEndpointId });
    }

    public Task<Wallet> GetWallet(Guid walletId)
    {
        return _connection.QuerySingleAsync<Wallet>("SELECT * FROM Wallets WHERE Id = @walletId", new { walletId });
    }

    public Task<int> GetNextNumberForId(Guid id)
    {
        return _connection.ExecuteScalarAsync<int>("SELECT * FROM IncrementNumberForId(@in_id);", new { in_id = id });
    }

    public async Task<DepositEndpoint> GetWalletRemainderDepositEndpoint(Guid walletId)
    {
        var referenceText = RemainderReferenceText;
        var remainderEndpoint = await _connection.QuerySingleOrDefaultAsync<DepositEndpoint?>("SELECT * FROM DepositEndpoints WHERE WalletId = @walletId AND ReferenceText = @referenceText", new { walletId, referenceText });

        if (remainderEndpoint is null)
        {
            var wallet = await GetWallet(walletId);
            var nextWalletPosition = await GetNextNumberForId(walletId);
            var publicKey = wallet.PrivateKey.Derive(nextWalletPosition).Neuter();
            remainderEndpoint = new DepositEndpoint
            {
                Id = Guid.NewGuid(),
                WalletId = walletId,
                WalletPosition = nextWalletPosition,
                PublicKey = publicKey,
                Owner = wallet.Owner,
                ReferenceText = referenceText,
                Endpoint = string.Empty
            };
            await CreateDepositEndpoint(remainderEndpoint);
        }

        return remainderEndpoint;
    }

    public async Task<IHDPrivateKey> GetPrivateKeyForSlice(Guid sliceId)
    {
        var keyInfo = await _connection.QuerySingleAsync<(IHDPrivateKey PrivateKey, int WalletPosition, int DepositEndpointPosition)>(
            @"SELECT w.PrivateKey, de.WalletPosition, s.DepositEndpointPosition
              FROM Slices s
              INNER JOIN DepositEndpoints de on s.DepositEndpointId = de.Id
              INNER JOIN Wallets w on de.WalletId = w.Id
              WHERE s.Id = @sliceId", new { sliceId });

        return keyInfo.PrivateKey.Derive(keyInfo.WalletPosition).Derive(keyInfo.DepositEndpointPosition);
    }
}
