using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit.Courier.Contracts;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server;

public interface IRegistryProcessBuilder
{
    RoutingSlip Build();
    Task Claim(Slice productionSlice, Slice consumptionSlice);
    Task<(Slice quantitySlice, Slice remainderSlice)> SplitSlice(Slice source, long quantity);
    void SetSliceStates(Dictionary<Guid, SliceState> newStates);
}
