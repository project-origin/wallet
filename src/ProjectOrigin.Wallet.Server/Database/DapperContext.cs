using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ProjectOrigin.Wallet.Server.Database;

public interface IDapperContext
{
    IDbConnection CreateConnection();
}

public class DapperContext : IDapperContext
{
    private readonly string? _connectionString;

    public DapperContext(IConfiguration configuration)
        : this(configuration.GetConnectionString("Database"))
    {
    }

    public DapperContext(string? connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}
