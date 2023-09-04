using System.Data;

namespace ProjectOrigin.WalletSystem.Server.Database;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
