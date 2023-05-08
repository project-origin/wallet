using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ProjectOrigin.Wallet.Server;

public class SomeBackgroundService : BackgroundService
{
    private readonly ILogger<SomeBackgroundService> _logger;
    private readonly string? _connectionString;

    public SomeBackgroundService(ILogger<SomeBackgroundService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("Database");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        await connection.ExecuteAsync(@"INSERT INTO MyTable(Foo) VALUES (@foo)", new { foo = Guid.NewGuid().ToString() });

        var myTables = await connection.QueryAsync<MyTable>("SELECT * FROM MyTable");

        _logger.LogInformation("Tables: {tables}", myTables);
    }
}

public record MyTable(int Id, string Foo);
