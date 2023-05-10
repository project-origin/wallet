
using System;
using ProjectOrigin.Wallet.Server.Repositories;

namespace ProjectOrigin.Wallet.Server.Database;

public class UnitOfWork : AbstractUnitOfWork
{
    private WalletRepository? _walletRepository;

    public UnitOfWork(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public WalletRepository WalletRepository
    {
        get
        {
            return _walletRepository ?? (_walletRepository = new WalletRepository(_transaction ?? throw new InvalidOperationException("Transaction does not exist.")));
        }
    }

    protected override void ResetUnitOfWork()
    {
        _walletRepository = null;
    }
}
