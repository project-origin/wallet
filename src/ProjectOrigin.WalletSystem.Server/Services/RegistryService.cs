using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using ProjectOrigin.WalletSystem.Server.Projections;

namespace ProjectOrigin.WalletSystem.Server.Services;

public class RegistryService : IRegistryService
{
    private IStreamProjector<GranularCertificate> _projector;

    public RegistryService(IStreamProjector<GranularCertificate> projector)
    {
        _projector = projector;
    }

    public async Task<GranularCertificate?> GetGranularCertificate(string registryName, Guid certificateId)
    {
        var channel = GetChannel(registryName);
        Registry.V1.RegistryService.RegistryServiceClient client = new Registry.V1.RegistryService.RegistryServiceClient(channel);

        var response = await client.GetStreamTransactionsAsync(new Registry.V1.GetStreamTransactionsRequest
        {
            StreamId = new Common.V1.Uuid { Value = certificateId.ToString() }
        });

        var granularCertificate = _projector.Project(response.Transactions);

        return granularCertificate;
    }

    private GrpcChannel GetChannel(string registryName)
    {

        throw new NotImplementedException();
        return GrpcChannel.ForAddress("https://localhost:5001");
    }
}

public interface IRegistryService
{
    Task<GranularCertificate?> GetGranularCertificate(string registryName, Guid certificateId);
}
