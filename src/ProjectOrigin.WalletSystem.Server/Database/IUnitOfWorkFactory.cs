namespace ProjectOrigin.WalletSystem.Server.Database;

public interface IUnitOfWorkFactory
{
    UnitOfWork Create();
}

public class UnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UnitOfWorkFactory(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public UnitOfWork Create()
    {
        return new UnitOfWork(_connectionFactory);
    }
}
