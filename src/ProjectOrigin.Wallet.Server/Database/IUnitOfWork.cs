using System;

namespace ProjectOrigin.Wallet.Server.Database;

public interface IUnitOfWork : IDisposable
{
    void Commit();
    void Rollback();
}
