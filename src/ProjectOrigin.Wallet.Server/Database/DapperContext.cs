using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ProjectOrigin.Wallet.Server.Database;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}

public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string? _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
        : this(configuration.GetConnectionString("Database"))
    {
    }

    public DbConnectionFactory(string? connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}
