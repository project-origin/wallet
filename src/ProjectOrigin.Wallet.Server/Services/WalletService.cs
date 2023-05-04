using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ProjectOrigin.Wallet.Server.Services;

public class WalletService : ProjectOrigin.Wallet.V1.WalletService.WalletServiceBase
{
    private readonly ILogger<WalletService> _logger;
    public WalletService(ILogger<WalletService> logger)
    {
        _logger = logger;
    }

    public override Task<V1.ReceiveResponse> ReceiveSlice(V1.ReceiveRequest request, Grpc.Core.ServerCallContext context)
    {
        throw new NotImplementedException("🌱");
    }
}
