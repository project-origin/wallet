using System;
using System.Threading.Tasks;
using Grpc.Core;
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

    public async Task<GetCertificateResult> GetGranularCertificate(string registryName, Guid certificateId)
    {
        var client = GetClient(registryName);
        if (client is null)
            return new GetCertificateResult.Failure(new Exception("Registry with name " + registryName + " not found in configuration."));

        try
        {
            var request = new Registry.V1.GetStreamTransactionsRequest
            {
                StreamId = new Common.V1.Uuid { Value = certificateId.ToString() }
            };

            var response = await client.GetStreamTransactionsAsync(request);
            var granularCertificate = _projector.Project(response.Transactions);
            return new GetCertificateResult.Success(granularCertificate);
        }
        catch (RpcException ex)
        {
            return new GetCertificateResult.TransientFailure(ex);
        }
        catch (NotSupportedException ex)
        {
            return new GetCertificateResult.Failure(ex);
        }
    }

    private Registry.V1.RegistryService.RegistryServiceClient? GetClient(string registryName)
    {
        if (!_options.Value.RegistryUrls.TryGetValue(registryName, out var registryUrl))
            return null;

        using var channel = GrpcChannel.ForAddress(registryUrl);
        return new Registry.V1.RegistryService.RegistryServiceClient(channel);
    }
}
