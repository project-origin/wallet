using System;
using System.Collections.Generic;
using System.Data;
using ProjectOrigin.WalletSystem.Server.Repositories;

namespace ProjectOrigin.WalletSystem.Server.Database;

public class UnitOfWork : IUnitOfWork
{
    public IWalletRepository WalletRepository => GetRepository(connection => new WalletRepository(connection));
    public ICertificateRepository CertificateRepository => GetRepository(connection => new CertificateRepository(connection));

    private readonly Dictionary<Type, object> _repositories = new Dictionary<Type, object>();
    private readonly Lazy<IDbConnection> _lazyConnection;
    private Lazy<IDbTransaction> _lazyTransaction;

    public UnitOfWork(IDbConnectionFactory connectionFactory)
    {
        _lazyConnection = new Lazy<IDbConnection>(() =>
        {
            var connection = connectionFactory.CreateConnection();
            connection.Open();
            return connection;
        });

        _lazyTransaction = new Lazy<IDbTransaction>(_lazyConnection.Value.BeginTransaction);
    }

    public void Commit()
    {
        if (!_lazyTransaction.IsValueCreated)
            return;

        try
        {
            _lazyTransaction.Value.Commit();
        }
        catch
        {
            _lazyTransaction.Value.Rollback();
            throw;
        }
        finally
        {
            ResetUnitOfWork();
        }
    }

    public void Rollback()
    {
        if (!_lazyTransaction.IsValueCreated)
            return;

        _lazyTransaction.Value.Rollback();

        ResetUnitOfWork();
    }

    public void Dispose()
    {
        if (_lazyTransaction.IsValueCreated)
        {
            _lazyTransaction.Value.Dispose();
        }

        if (_lazyConnection.IsValueCreated)
        {
            _lazyConnection.Value.Dispose();
        }
    }

    public T GetRepository<T>(Func<IDbConnection, T> factory) where T : class
    {
        if (_repositories.TryGetValue(typeof(T), out var foundRepository))
        {
            return (T)foundRepository;
        }
        else
        {
            var newRepository = factory(_lazyTransaction.Value.Connection ?? throw new InvalidOperationException("Transaction is null."));
            _repositories.Add(typeof(T), newRepository);
            return newRepository;
        }
    }

    private void ResetUnitOfWork()
    {
        if (_lazyTransaction.IsValueCreated)
            _lazyTransaction.Value.Dispose();

        _lazyTransaction = new Lazy<IDbTransaction>(_lazyConnection.Value.BeginTransaction);

        _repositories.Clear();
    }
}
