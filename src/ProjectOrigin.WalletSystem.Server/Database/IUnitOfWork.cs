using System;
using ProjectOrigin.WalletSystem.Server.Repositories;

namespace ProjectOrigin.WalletSystem.Server.Database;

public interface IUnitOfWork : IDisposable
{
    void Commit();
    void Rollback();

    IWalletRepository WalletRepository { get; }
    ICertificateRepository CertificateRepository { get; }
}
