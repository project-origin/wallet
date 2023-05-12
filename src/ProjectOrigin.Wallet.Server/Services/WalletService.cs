using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using ProjectOrigin.Wallet.Server.Database;
using ProjectOrigin.Wallet.Server.Models;

namespace ProjectOrigin.Wallet.Server.Services;

public class ExternalWalletService : ProjectOrigin.Wallet.V1.ExternalWalletService.ExternalWalletServiceBase
{
    private readonly ILogger<ExternalWalletService> _logger;
    private readonly IDbConnectionFactory _connectionFactory;

    public ExternalWalletService(ILogger<ExternalWalletService> logger, IDbConnectionFactory connectionFactory)
    {
        _logger = logger;
        _connectionFactory = connectionFactory;
    }

    public override async Task<V1.ReceiveResponse> ReceiveSlice(V1.ReceiveRequest request, Grpc.Core.ServerCallContext context)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(@"INSERT INTO MyTable(Foo) VALUES (@foo)", new { foo = Guid.NewGuid().ToString() });

        var myTables = await connection.QueryAsync<MyTable>("SELECT * FROM MyTable");

        throw new NotImplementedException("🌱");
    }
}
