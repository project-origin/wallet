using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using ProjectOrigin.Wallet.Server.Database;
using ProjectOrigin.Wallet.Server.Models;

namespace ProjectOrigin.Wallet.Server.Services;

public class WalletService : ProjectOrigin.Wallet.V1.WalletService.WalletServiceBase
{
    private readonly ILogger<WalletService> _logger;
    private readonly IDapperContext _context;

    public WalletService(ILogger<WalletService> logger, IDapperContext context)
    {
        _logger = logger;
        _context = context;
    }

    public override async Task<V1.ReceiveResponse> ReceiveSlice(V1.ReceiveRequest request, Grpc.Core.ServerCallContext context)
    {
        using var connection = _context.CreateConnection();

        await connection.ExecuteAsync(@"INSERT INTO MyTable(Foo) VALUES (@foo)", new { foo = Guid.NewGuid().ToString() });

        var myTables = await connection.QueryAsync<MyTable>("SELECT * FROM MyTable");

        throw new NotImplementedException("🌱");
    }
}
