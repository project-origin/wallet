using System.Data;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ProjectOrigin.Vault.Database.Postgres;

public class PostgresConnectionFactory : IDbConnectionFactory
{
    private readonly PostgresOptions _databaseOptions;

    public PostgresConnectionFactory(IOptions<PostgresOptions> databaseOptions)
    {
        _databaseOptions = databaseOptions.Value;
    }

    public IDbConnection CreateConnection() => new NpgsqlConnection(_databaseOptions.ConnectionString);
}
