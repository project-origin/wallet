using System;
using MassTransit;
using Microsoft.Extensions.Options;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Options;

namespace ProjectOrigin.Vault;

public interface IRegistryProcessBuilderFactory
{
    IRegistryProcessBuilder Create(Guid routingSlipId, string owner, IUnitOfWork unitOfWork);
}

public class RegistryProcessBuilderFactory : IRegistryProcessBuilderFactory
{
    private readonly IEndpointNameFormatter _endpointNameFormatter;
    private readonly IOptions<NetworkOptions> _networkOptions;

    public RegistryProcessBuilderFactory(IEndpointNameFormatter endpointNameFormatter, IOptions<NetworkOptions> networkOptions)
    {
        _endpointNameFormatter = endpointNameFormatter;
        _networkOptions = networkOptions;
    }

    public IRegistryProcessBuilder Create(Guid routingSlipId, string owner, IUnitOfWork unitOfWork)
    {
        return new RegistryProcessBuilder(unitOfWork, _endpointNameFormatter, routingSlipId, _networkOptions, owner);
    }
}
