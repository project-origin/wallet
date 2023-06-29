using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;
using ProjectOrigin.WalletSystem.Server.Options;
using ProjectOrigin.WalletSystem.Server.Projections;

namespace ProjectOrigin.WalletSystem.Server.Services;

public class RegistryService : IRegistryService
{
    private readonly IOptions<RegistryOptions> _options;
    private readonly IStreamProjector<GranularCertificate> _projector;

    public RegistryService(IOptions<RegistryOptions> options, IStreamProjector<GranularCertificate> projector)
    {
        _options = options;
        _projector = projector;
    }

    public async Task<GranularCertificate?> GetGranularCertificate(string registryName, Guid certificateId)
    {
        using var channel = GetChannel(registryName);
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
        var registryUrl = _options.Value.RegistryUrls[registryName];
        return GrpcChannel.ForAddress(registryUrl);
    }
}
