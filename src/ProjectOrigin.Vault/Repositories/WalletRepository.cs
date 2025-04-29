using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.Vault.Models;

namespace ProjectOrigin.Vault.Repositories;

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

    public Task<Wallet?> GetWallet(Guid walletId)
    {
        return _connection.QuerySingleOrDefaultAsync<Wallet>(
            @"SELECT *
              FROM Wallets
              WHERE Id = @walletId",
            new
            {
                walletId
            });
    }

    public Task<Wallet?> GetWallet(string owner)
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

    public async Task DisableWallet(Guid walletId, DateTimeOffset disabledDateUtc)
    {
        var rowsChanged = await _connection.ExecuteAsync(
            @"UPDATE wallets
              SET disabled = @disabledDateUtc
              WHERE id = @walletId",
            new
            {
                walletId,
                disabledDateUtc
            });

        if (rowsChanged != 1)
            throw new InvalidOperationException($"Wallet with id {walletId} could not be found");
    }

    public async Task<WalletEndpoint> CreateWalletEndpoint(Guid walletId)
    {
        var position = await GetNextNumberForId(walletId);

        var wallet = await GetWallet(walletId) ?? throw new InvalidOperationException("Wallet not found");
        var key = wallet.PrivateKey.Derive(position).Neuter();

        var newEndpoint = new WalletEndpoint
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

    public async Task<ExternalEndpoint> CreateExternalEndpoint(string owner, IHDPublicKey ownerPublicKey, string referenceText, string endpoint)
    {
        var newEndpoint = new ExternalEndpoint
        {
            Id = Guid.NewGuid(),
            Owner = owner,
            PublicKey = ownerPublicKey,
            ReferenceText = referenceText,
            Endpoint = endpoint
        };

        await _connection.ExecuteAsync(
            @"INSERT INTO external_endpoints(id, owner, public_key, reference_text, endpoint)
              VALUES (@id, @owner, @publicKey, @referenceText, @endpoint)",
            newEndpoint);

        return newEndpoint;
    }

    public async Task<WalletEndpoint?> GetWalletEndpoint(IHDPublicKey publicKey)
    {
        return await _connection.QuerySingleOrDefaultAsync<WalletEndpoint>(
            @"SELECT *
              FROM wallet_endpoints
              WHERE public_key = @publicKey",
            new
            {
                publicKey
            });
    }

    public Task<WalletEndpoint> GetWalletEndpoint(Guid endpointId)
    {
        return _connection.QuerySingleAsync<WalletEndpoint>(
            @"SELECT *
              FROM wallet_endpoints
              WHERE id = @endpointId",
            new
            {
                endpointId
            });
    }

    public Task<ExternalEndpoint> GetExternalEndpoint(Guid endpointId)
    {
        return _connection.QuerySingleAsync<ExternalEndpoint>(
            @"SELECT *
              FROM external_endpoints
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

    public async Task<WalletEndpoint> GetWalletRemainderEndpoint(Guid walletId)
    {
        var endpoint = await _connection.QuerySingleOrDefaultAsync<WalletEndpoint?>(
            @"SELECT *
              FROM wallet_endpoints
              WHERE wallet_id = @walletId
                AND is_remainder_endpoint is TRUE",
            new
            {
                walletId
            });

        if (endpoint is null)
        {
            var wallet = await GetWallet(walletId) ?? throw new InvalidOperationException("Wallet not found");
            var nextWalletPosition = await GetNextNumberForId(walletId);
            var publicKey = wallet.PrivateKey.Derive(nextWalletPosition).Neuter();

            endpoint = new WalletEndpoint
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
        var keyInfo = await _connection.QuerySingleAsync<(IHDPrivateKey PrivateKey, int WalletPosition, int EndpointPosition)>(
            @"SELECT w.private_key, re.wallet_position, s.wallet_endpoint_position
              FROM wallet_slices s
              INNER JOIN wallet_endpoints re
                ON s.wallet_endpoint_id = re.id
              INNER JOIN wallets w
                ON re.wallet_id = w.id
              WHERE s.id = @sliceId",
            new
            {
                sliceId
            });

        return keyInfo.PrivateKey.Derive(keyInfo.WalletPosition).Derive(keyInfo.EndpointPosition);
    }

    private Task<int> CreateReceiveEndpoint(WalletEndpoint endpoint)
    {
        return _connection.ExecuteAsync(
            @"INSERT INTO wallet_endpoints(id, wallet_id, wallet_position, public_key, is_remainder_endpoint)
              VALUES (@id, @walletId, @walletPosition, @publicKey, @isRemainderEndpoint)",
              endpoint);
    }

}
