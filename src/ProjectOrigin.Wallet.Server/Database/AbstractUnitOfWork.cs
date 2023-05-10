using System.Data;

namespace ProjectOrigin.Wallet.Server.Database;

public abstract class AbstractUnitOfWork : IUnitOfWork
{
    private IDbConnection? _connection;
    protected IDbTransaction? _transaction;

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
            _transaction.Dispose();
            _transaction = _connection.BeginTransaction();
        }
    }

    public void Rollback()
    {
        _transaction.Rollback();
        _transaction.Dispose();
        _transaction = _connection.BeginTransaction();
    }

    public void Dispose()
    {
        if (_transaction != null)
        {
            _transaction.Dispose();
            _transaction = null;
        }

        if (_connection != null)
        {
            _connection.Dispose();
            _connection = null;
        }
    }

    protected abstract void ResetUnitOfWork();
}
