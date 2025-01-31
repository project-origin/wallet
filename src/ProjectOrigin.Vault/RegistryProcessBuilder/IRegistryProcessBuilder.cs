using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit.Courier.Contracts;
using ProjectOrigin.Vault.Activities;
using ProjectOrigin.Vault.Models;

namespace ProjectOrigin.Vault;

public interface IRegistryProcessBuilder
{
    RoutingSlip Build();
    Task Claim(WalletSlice productionSlice, WalletSlice consumptionSlice);
    Task<(WalletSlice quantitySlice, WalletSlice remainderSlice)> SplitSlice(WalletSlice source, long quantity, RequestStatusArgs requestStatusArgs);
    void SetWalletSliceStates(Dictionary<Guid, WalletSliceState> newStates);
}
