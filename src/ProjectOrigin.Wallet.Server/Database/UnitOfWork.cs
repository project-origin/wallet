
using ProjectOrigin.Wallet.Server.Repositories;

namespace ProjectOrigin.Wallet.Server.Database;

public class UnitOfWork : AbstractUnitOfWork
{
    public UnitOfWork(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public WalletRepository WalletRepository => GetRepository<WalletRepository>(transaction => new WalletRepository(transaction));
}
