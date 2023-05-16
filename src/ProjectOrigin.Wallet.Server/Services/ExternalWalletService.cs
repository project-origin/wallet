using System.Threading.Tasks;
using Grpc.Core;
using ProjectOrigin.Wallet.V1;

namespace ProjectOrigin.Wallet.Server.Services
{
    internal class ExternalWalletService : ProjectOrigin.Wallet.V1.ExternalWalletService.ExternalWalletServiceBase
    {


        public override Task<ReceiveResponse> ReceiveSlice(ReceiveRequest request, ServerCallContext context)
        {
            return base.ReceiveSlice(request, context);
        }
    }
}
