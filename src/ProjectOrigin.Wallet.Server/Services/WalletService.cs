using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ProjectOrigin.Wallet.Server.Services;

public class ExternalWalletService : ProjectOrigin.Wallet.V1.ExternalWalletService.ExternalWalletServiceBase
{
    private readonly ILogger<ExternalWalletService> _logger;
    public ExternalWalletService(ILogger<ExternalWalletService> logger)
    {
        _logger = logger;
    }

    public override Task<V1.ReceiveResponse> ReceiveSlice(V1.ReceiveRequest request, Grpc.Core.ServerCallContext context)
    {
        throw new NotImplementedException("🌱");
    }
}
