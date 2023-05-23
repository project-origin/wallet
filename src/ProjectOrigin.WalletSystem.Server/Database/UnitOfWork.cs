using ProjectOrigin.WalletSystem.Server.Repositories;

namespace ProjectOrigin.WalletSystem.Server.Database;

public class UnitOfWork : AbstractUnitOfWork
{
    public UnitOfWork(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public WalletRepository WalletRepository => GetRepository(connection => new WalletRepository(connection));
    public CertificateRepository CertificateRepository => GetRepository(connection => new CertificateRepository(connection));
}
