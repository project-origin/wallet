using System.Data;

namespace ProjectOrigin.Vault.Database;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
