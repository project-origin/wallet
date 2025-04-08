using System;
using System.Threading.Tasks;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.Vault.Models;

namespace ProjectOrigin.Vault.Repositories;

public interface IWalletRepository
{
    Task<int> Create(Wallet wallet);
    Task<Wallet?> GetWallet(Guid walletId);
    Task<Wallet?> GetWallet(string owner);

    Task<WalletEndpoint> CreateWalletEndpoint(Guid walletId);
    Task<WalletEndpoint?> GetWalletEndpoint(IHDPublicKey publicKey);
    Task<WalletEndpoint> GetWalletEndpoint(Guid endpointId);
    Task<WalletEndpoint> GetWalletRemainderEndpoint(Guid walletId);

    Task<int> GetNextNumberForId(Guid id);
    Task<IHDPrivateKey> GetPrivateKeyForSlice(Guid sliceId);

    Task<ExternalEndpoint> CreateExternalEndpoint(string owner, IHDPublicKey ownerPublicKey, string referenceText, string endpoint);
    Task<ExternalEndpoint> GetExternalEndpoint(Guid endpointId);

    Task DisableWallet(Guid walletId, DateTimeOffset disabledDate);
}
