using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ProjectOrigin.Wallet.Server;

public class SomeBackgroundService : BackgroundService
{
    private readonly ILogger<SomeBackgroundService> _logger;

    public SomeBackgroundService(ILogger<SomeBackgroundService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var connection = new NpgsqlConnection("Host=localhost; Port=5432; Database=postgres; Username=admin; Password=admin;");

        await connection.ExecuteAsync(@"INSERT INTO MyTable(Foo) VALUES (@foo)", new { foo = Guid.NewGuid().ToString()});

        var myTables = await connection.QueryAsync<MyTable>("SELECT * FROM MyTable");

        _logger.LogInformation("Tables: {tables}", myTables);
    }
}

public class MyTable
{
    public int Id { get; set; }
    public string Foo { get; set; } = "";
}
