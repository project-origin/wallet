using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Courier.Contracts;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server;

public interface IRegistryProcessBuilder
{
    public void AddActivity<T, TArguments>(TArguments arguments)
        where T : class, IExecuteActivity<TArguments>
        where TArguments : class;

    public RoutingSlip Build();

    Task Claim(Slice productionSlice, Slice consumptionSlice);
    Task<(Slice quantitySlice, Slice remainderSlice)> SplitSlice(Slice source, long quantity);
    void SetSliceStates(Dictionary<Guid, SliceState> newStates);

}
