using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public class WalletRepository : IWalletRepository
{
    private readonly IDbConnection _connection;

    public WalletRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public Task<int> Create(Wallet wallet)
    {
        return _connection.ExecuteAsync(
            @"INSERT INTO wallets(id, owner, private_key)
              VALUES (@id, @owner, @privateKey)",
            wallet);
    }

    public Task<Wallet> GetWallet(Guid walletId)
    {
        return _connection.QuerySingleAsync<Wallet>(
            @"SELECT *
              FROM Wallets
              WHERE Id = @walletId",
            new
            {
                walletId
            });
    }

    public Task<Wallet?> GetWalletByOwner(string owner)
    {
        return _connection.QuerySingleOrDefaultAsync<Wallet?>(
            @"SELECT *
              FROM wallets
              WHERE owner = @owner",
            new
            {
                owner
            });
    }

    public async Task<ReceiveEndpoint> CreateReceiveEndpoint(Guid walletId)
    {
        var position = await GetNextNumberForId(walletId);

        var wallet = await GetWallet(walletId);
        var key = wallet.PrivateKey.Derive(position).Neuter();

        var newEndpoint = new ReceiveEndpoint
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            WalletPosition = position,
            PublicKey = key,
            IsRemainderEndpoint = false
        };

        await CreateReceiveEndpoint(newEndpoint);

        return newEndpoint;
    }

    public async Task<DepositEndpoint> CreateDepositEndpoint(string owner, IHDPublicKey ownerPublicKey, string referenceText, string endpoint)
    {
        var newEndpoint = new DepositEndpoint
        {
            Id = Guid.NewGuid(),
            Owner = owner,
            PublicKey = ownerPublicKey,
            ReferenceText = referenceText,
            Endpoint = endpoint
        };

        await _connection.ExecuteAsync(
            @"INSERT INTO deposit_endpoints(id, owner, public_key, reference_text, endpoint)
              VALUES (@id, @owner, @publicKey, @referenceText, @endpoint)",
            newEndpoint);

        return newEndpoint;
    }

    public async Task<ReceiveEndpoint?> GetReceiveEndpoint(IHDPublicKey publicKey)
    {
        return await _connection.QuerySingleOrDefaultAsync<ReceiveEndpoint>(
            @"SELECT *
              FROM receive_endpoints
              WHERE public_key = @publicKey",
            new
            {
                publicKey
            });
    }

    public Task<ReceiveEndpoint> GetReceiveEndpoint(Guid endpointId)
    {
        return _connection.QuerySingleAsync<ReceiveEndpoint>(
            @"SELECT *
              FROM receive_endpoints
              WHERE id = @endpointId",
            new
            {
                endpointId
            });
    }

    public Task<DepositEndpoint> GetDepositEndpoint(Guid endpointId)
    {
        return _connection.QuerySingleAsync<DepositEndpoint>(
            @"SELECT *
              FROM deposit_endpoints
              WHERE id = @endpointId",
            new
            {
                endpointId
            });
    }

    public Task<int> GetNextNumberForId(Guid id)
    {
        return _connection.ExecuteScalarAsync<int>(
            @"SELECT *
              FROM IncrementNumberForId(@id);",
            new
            {
                id
            });
    }

    public async Task<ReceiveEndpoint> GetWalletRemainderEndpoint(Guid walletId)
    {
        var endpoint = await _connection.QuerySingleOrDefaultAsync<ReceiveEndpoint?>(
            @"SELECT *
              FROM receive_endpoints
              WHERE wallet_id = @walletId
                AND is_remainder_endpoint is TRUE",
            new
            {
                walletId
            });

        if (endpoint is null)
        {
            var wallet = await GetWallet(walletId);
            var nextWalletPosition = await GetNextNumberForId(walletId);
            var publicKey = wallet.PrivateKey.Derive(nextWalletPosition).Neuter();

            endpoint = new ReceiveEndpoint
            {
                Id = Guid.NewGuid(),
                WalletId = walletId,
                WalletPosition = nextWalletPosition,
                PublicKey = publicKey,
                IsRemainderEndpoint = true
            };

            await CreateReceiveEndpoint(endpoint);
        }

        return endpoint;
    }

    public async Task<IHDPrivateKey> GetPrivateKeyForSlice(Guid sliceId)
    {
        var keyInfo = await _connection.QuerySingleAsync<(IHDPrivateKey PrivateKey, int WalletPosition, int DepositEndpointPosition)>(
            @"SELECT w.PrivateKey, e.WalletPosition, s.DepositEndpointPosition
              FROM received_slices s
              INNER JOIN receive_endpoints e
                ON s.receive_endpoint_id = e.id
              INNER JOIN wallets w
                ON de.wallet_id = w.id
              WHERE s.id = @sliceId",
            new
            {
                sliceId
            });

        return keyInfo.PrivateKey.Derive(keyInfo.WalletPosition).Derive(keyInfo.DepositEndpointPosition);
    }

    private Task CreateReceiveEndpoint(ReceiveEndpoint endpoint)
    {
        return _connection.ExecuteAsync(
            @"INSERT INTO receive_endpoints(id, wallet_id, wallet_position, public_key, is_remainder_endpoint)
              VALUES (@id, @walletId, @walletPosition, @publicKey, @isRemainderEndpoint)",
              endpoint);
    }

}
