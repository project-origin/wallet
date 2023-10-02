using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit.Courier.Contracts;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server;

public interface IRegistryProcessBuilder
{
    RoutingSlip Build();
    Task Claim(ReceivedSlice productionSlice, ReceivedSlice consumptionSlice);
    Task<(ReceivedSlice quantitySlice, ReceivedSlice remainderSlice)> SplitSlice(ReceivedSlice source, long quantity);
    void SetSliceStates(Dictionary<Guid, ReceivedSliceState> newStates);
}
