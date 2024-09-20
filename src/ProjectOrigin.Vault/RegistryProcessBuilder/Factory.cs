using System;
using MassTransit;
using ProjectOrigin.Vault.Database;

namespace ProjectOrigin.Vault;

public interface IRegistryProcessBuilderFactory
{
    IRegistryProcessBuilder Create(Guid routingSlipId, string owner, IUnitOfWork unitOfWork);
}

public class RegistryProcessBuilderFactory : IRegistryProcessBuilderFactory
{
    private readonly IEndpointNameFormatter _endpointNameFormatter;

    public RegistryProcessBuilderFactory(IEndpointNameFormatter endpointNameFormatter)
    {
        _endpointNameFormatter = endpointNameFormatter;
    }

    public IRegistryProcessBuilder Create(Guid routingSlipId, string owner, IUnitOfWork unitOfWork)
    {
        return new RegistryProcessBuilder(unitOfWork, _endpointNameFormatter, routingSlipId, owner);
    }
}
