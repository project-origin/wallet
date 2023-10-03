using System;
using System.Threading.Tasks;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public interface IWalletRepository
{
    Task<int> Create(Wallet wallet);
    Task<Wallet> GetWallet(Guid walletId);
    Task<Wallet?> GetWallet(string owner);

    Task<WalletEndpoint> CreateWalletEndpoint(Guid walletId);
    Task<WalletEndpoint?> GetWalletEndpoint(IHDPublicKey publicKey);
    Task<WalletEndpoint> GetWalletEndpoint(Guid endpointId);
    Task<WalletEndpoint> GetWalletRemainderEndpoint(Guid walletId);

    Task<int> GetNextNumberForId(Guid id);
    Task<IHDPrivateKey> GetPrivateKeyForSlice(Guid sliceId);

    Task<OutboxEndpoint> CreateOutboxEndpoint(string owner, IHDPublicKey ownerPublicKey, string referenceText, string endpoint);
    Task<OutboxEndpoint> GetOutboxEndpoint(Guid endpointId);
}
