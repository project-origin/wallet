using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit.Courier.Contracts;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server;

public interface IRegistryProcessBuilder
{
    RoutingSlip Build();
    Task Claim(WalletSlice productionSlice, WalletSlice consumptionSlice);
    Task<(WalletSlice quantitySlice, WalletSlice remainderSlice)> SplitSlice(WalletSlice source, long quantity);
    void SetWalletSliceStates(Dictionary<Guid, WalletSliceState> newStates);
}
