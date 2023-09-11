using System;
using System.Threading.Tasks;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public interface IWalletRepository
{
    Task<int> Create(Wallet wallet);
    Task<Wallet?> GetWalletByOwner(string owner);
    Task<DepositEndpoint> CreateDepositEndpoint(Guid walletId, string referenceText);
    Task<DepositEndpoint> CreateReceiverDepositEndpoint(string owner, IHDPublicKey ownerPublicKey, string referenceText, string endpoint);
    Task<DepositEndpoint?> GetDepositEndpointFromPublicKey(IHDPublicKey publicKey);
    Task<DepositEndpoint> GetDepositEndpoint(Guid depositEndpointId);
    Task<Wallet> GetWallet(Guid walletId);
    Task<int> GetNextNumberForId(Guid id);
    Task<DepositEndpoint> GetWalletRemainderDepositEndpoint(Guid walletId);
    Task<IHDPrivateKey> GetPrivateKeyForSlice(Guid sliceId);
}
