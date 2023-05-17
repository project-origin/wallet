
using System;
using ProjectOrigin.Wallet.Server.Repositories;

namespace ProjectOrigin.Wallet.Server.Database;

public class UnitOfWork : AbstractUnitOfWork
{
    public UnitOfWork(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public WalletRepository WalletRepository => GetRepository<WalletRepository>(connection => new WalletRepository(connection));
    public CertificateRepository CertficateRepository => GetRepository<CertificateRepository>(connection => new CertificateRepository(connection));
}
