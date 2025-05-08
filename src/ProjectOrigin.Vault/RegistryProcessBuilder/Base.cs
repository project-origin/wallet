using System;
using System.Collections.Generic;
using MassTransit;
using MassTransit.Courier.Contracts;
using Microsoft.Extensions.Options;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.Vault.Activities;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Options;

namespace ProjectOrigin.Vault;

public partial class RegistryProcessBuilder : IRegistryProcessBuilder
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEndpointNameFormatter _formatter;
    private readonly Guid _routingSlipId;
    private readonly string _owner;
    private readonly IRoutingSlipBuilder _slipBuilder;
    private readonly IOptions<NetworkOptions> _networkOptions;

    public RegistryProcessBuilder(IUnitOfWork unitOfWork, IEndpointNameFormatter formatter, Guid routingSlipId,
        IOptions<NetworkOptions> networkOptions, string owner)
    {
        _unitOfWork = unitOfWork;
        _formatter = formatter;
        _routingSlipId = routingSlipId;
        _owner = owner;
        _networkOptions = networkOptions;
        _slipBuilder = new RoutingSlipBuilder(routingSlipId);
    }

    internal void AddActivity<T, TArguments>(TArguments arguments)
        where T : class, IExecuteActivity<TArguments>
        where TArguments : class
    {
        var uri = new Uri($"exchange:{_formatter.ExecuteActivity<T, TArguments>()}");
        _slipBuilder.AddActivity(typeof(T).Name, uri, arguments);

        _slipBuilder.AddSubscription(
            uri,
            RoutingSlipEvents.Faulted | RoutingSlipEvents.ActivityCompensationFailed |
            RoutingSlipEvents.ActivityFaulted | RoutingSlipEvents.CompensationFailed);
    }

    public RoutingSlip Build()
    {
        return _slipBuilder.Build();
    }

    private void AddRegistryTransactionActivity(Transaction transaction, Guid sliceId,
        RequestStatusArgs? requestStatusArgs)
    {
        AddActivity<SendRegistryTransactionActivity, SendRegistryTransactionArguments>(
            new SendRegistryTransactionArguments()
            {
                Transaction = transaction
            });

        AddActivity<WaitCommittedRegistryTransactionActivity, WaitCommittedTransactionArguments>(
            new WaitCommittedTransactionArguments()
            {
                RegistryName = transaction.Header.FederatedStreamId.Registry,
                TransactionId = transaction.ToShaId(),
                CertificateId = new Guid(transaction.Header.FederatedStreamId.StreamId.Value),
                SliceId = sliceId,
                RequestStatusArgs = requestStatusArgs
            });
    }

    public void SetWalletSliceStates(Dictionary<Guid, WalletSliceState> newStates, RequestStatusArgs requestStatusArgs)
    {
        AddActivity<UpdateSliceStateActivity, UpdateSliceStateArguments>(
            new UpdateSliceStateArguments
            {
                SliceStates = newStates,
                RequestStatusArgs = requestStatusArgs
            });
    }
}
