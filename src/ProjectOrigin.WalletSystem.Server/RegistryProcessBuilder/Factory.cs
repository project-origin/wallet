using System;
using MassTransit;
using ProjectOrigin.WalletSystem.Server.Database;

namespace ProjectOrigin.WalletSystem.Server;

public interface IRegistryProcessBuilderFactory
{
    IRegistryProcessBuilder Create(Guid routingSlipId, IUnitOfWork unitOfWork);
}

public class RegistryProcessBuilderFactory : IRegistryProcessBuilderFactory
{
    private readonly IEndpointNameFormatter _endpointNameFormatter;

    public RegistryProcessBuilderFactory(IEndpointNameFormatter endpointNameFormatter)
    {
        _endpointNameFormatter = endpointNameFormatter;
    }

    public IRegistryProcessBuilder Create(Guid routingSlipId, IUnitOfWork unitOfWork)
    {
        return new RegistryProcessBuilder(unitOfWork, _endpointNameFormatter, routingSlipId);
    }
}
