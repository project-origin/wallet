using System;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;
using ProjectOrigin.Vault.Options;
using ProjectOrigin.Vault.Projections;

namespace ProjectOrigin.Vault.Services;

public class RegistryService : IRegistryService
{
    private readonly IOptions<NetworkOptions> _options;
    private readonly IStreamProjector<GranularCertificate> _projector;

    public RegistryService(IOptions<NetworkOptions> options, IStreamProjector<GranularCertificate> projector)
    {
        _options = options;
        _projector = projector;
    }

    public async Task<GetCertificateResult> GetGranularCertificate(string registryName, Guid certificateId)
    {
        if (!_options.Value.Registries.TryGetValue(registryName, out var registryInfo))
            return new GetCertificateResult.Failure(new ArgumentException("Registry with name " + registryName + " not found in configuration.", nameof(registryName)));

        using var channel = GrpcChannel.ForAddress(registryInfo.Url);
        var client = new Registry.V1.RegistryService.RegistryServiceClient(channel);

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
        catch (ProjectionException ex)
        {
            return new GetCertificateResult.Failure(ex);
        }
    }
}
