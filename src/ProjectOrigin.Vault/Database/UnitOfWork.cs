using System;
using System.Collections.Generic;
using System.Data;
using MassTransit.Futures.Contracts;
using ProjectOrigin.Vault.Repositories;

namespace ProjectOrigin.Vault.Database;

public class UnitOfWork : IUnitOfWork, IDisposable
{
    public IWalletRepository WalletRepository => GetRepository(connection => new WalletRepository(connection));
    public ICertificateRepository CertificateRepository => GetRepository(connection => new CertificateRepository(connection));
    public ITransferRepository TransferRepository => GetRepository(connection => new TransferRepository(connection));
    public IClaimRepository ClaimRepository => GetRepository(connection => new ClaimRepository(connection));
    public IOutboxMessageRepository OutboxMessageRepository => GetRepository(connection => new OutboxMessageRepository(connection));
    public IRequestStatusRepository RequestStatusRepository => GetRepository(connection => new RequestStatusRepository(connection));
    public IWithdrawnCursorRepository WithdrawnCursorRepository => GetRepository(connection => new WithdrawnCursorRepository(connection));
    public IJobExecutionRepository JobExecutionRepository => GetRepository(connection => new JobExecutionRepository(connection));

    private readonly Dictionary<Type, object> _repositories = new Dictionary<Type, object>();
    private readonly Lazy<IDbConnection> _lazyConnection;
    private Lazy<IDbTransaction> _lazyTransaction;
    private bool _disposed = false;

    public UnitOfWork(IDbConnectionFactory connectionFactory)
    {
        _lazyConnection = new Lazy<IDbConnection>(() =>
        {
            var connection = connectionFactory.CreateConnection();
            connection.Open();
            return connection;
        });

        _lazyTransaction = new Lazy<IDbTransaction>(() =>
        {
            if (_lazyConnection.IsValueCreated)
                return _lazyConnection.Value.BeginTransaction();
            throw new InvalidOperationException("Connection must be opened before creating a transaction.");
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

    public T GetRepository<T>(Func<IDbConnection, T> factory) where T : class
    {
        if (_repositories.TryGetValue(typeof(T), out var foundRepository))
        {
            return (T)foundRepository;
        }
        else
        {
            if (!_lazyConnection.IsValueCreated)
            {
                var connection = _lazyConnection.Value;
            }

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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~UnitOfWork() => Dispose(false);

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;

            if (_lazyTransaction.IsValueCreated)
            {
                _lazyTransaction.Value.Dispose();
            }

            if (_lazyConnection.IsValueCreated)
            {
                _lazyConnection.Value.Dispose();
            }
        }

        _repositories.Clear();
    }
}
