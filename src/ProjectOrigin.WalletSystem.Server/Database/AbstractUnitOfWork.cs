using System;
using System.Collections.Generic;
using System.Data;

namespace ProjectOrigin.WalletSystem.Server.Database;

public abstract class AbstractUnitOfWork : IUnitOfWork
{
    private IDbConnection _connection;
    protected IDbTransaction _transaction;
    protected Dictionary<Type, object> _repositories = new Dictionary<Type, object>();

    public AbstractUnitOfWork(IDbConnectionFactory connectionFactory)
    {
        _connection = connectionFactory.CreateConnection();
        _connection.Open();
        _transaction = _connection.BeginTransaction();
    }

    public void Commit()
    {
        try
        {
            _transaction.Commit();
        }
        catch
        {
            _transaction.Rollback();
            throw;
        }
        finally
        {
            ResetUnitOfWork();
        }
    }

    public void Rollback()
    {
        _transaction.Rollback();
        ResetUnitOfWork();
    }

    public void Dispose()
    {
        if (_transaction != null)
        {
            _transaction.Dispose();
        }

        if (_connection != null)
        {
            _connection.Dispose();
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
            var newRepository = factory(_transaction.Connection ?? throw new InvalidOperationException("Transaction is null."));
            _repositories.Add(typeof(T), newRepository);
            return newRepository;
        }
    }

    private void ResetUnitOfWork()
    {
        _transaction.Dispose();
        _transaction = _connection.BeginTransaction();
        _repositories.Clear();
    }
}
