using System;

namespace ProjectOrigin.WalletSystem.Server.Database;

public interface IUnitOfWork : IDisposable
{
    void Commit();
    void Rollback();
}
