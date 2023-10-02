using System;
using System.Threading.Tasks;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public interface IWalletRepository
{
    Task<int> Create(Wallet wallet);
    Task<Wallet?> GetWalletByOwner(string owner);
    Task<ReceiveEndpoint> CreateReceiveEndpoint(Guid walletId);
    Task<DepositEndpoint> CreateDepositEndpoint(string owner, IHDPublicKey ownerPublicKey, string referenceText, string endpoint);
    Task<ReceiveEndpoint?> GetReceiveEndpoint(IHDPublicKey publicKey);
    Task<ReceiveEndpoint> GetReceiveEndpoint(Guid depositEndpointId);
    Task<Wallet> GetWallet(Guid walletId);
    Task<int> GetNextNumberForId(Guid id);
    Task<ReceiveEndpoint> GetWalletRemainderEndpoint(Guid walletId);
    Task<IHDPrivateKey> GetPrivateKeyForSlice(Guid sliceId);
    Task<DepositEndpoint> GetDepositEndpoint(Guid receiverDepositEndpointId);
}
