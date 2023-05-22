using System;
using System.Collections.Generic;
using System.Data;

namespace ProjectOrigin.Wallet.Server.Database;

public abstract class AbstractUnitOfWork : IUnitOfWork
{
    private Lazy<IDbConnection> _lazyConnection;
    private Lazy<IDbTransaction> _lazyTransaction;
    protected Dictionary<Type, object> _repositories = new Dictionary<Type, object>();

    public AbstractUnitOfWork(IDbConnectionFactory connectionFactory)
    {
        _lazyConnection = new Lazy<IDbConnection>(() =>
        {
            var connection = connectionFactory.CreateConnection();
            connection.Open();
            return connection;
        });
        _lazyTransaction = new Lazy<IDbTransaction>(() =>
        {
            return _lazyConnection.Value.BeginTransaction();
        });
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

        _lazyTransaction = new Lazy<IDbTransaction>(() =>
        {
            return _lazyConnection.Value.BeginTransaction();
        });

        _repositories.Clear();
    }
}
