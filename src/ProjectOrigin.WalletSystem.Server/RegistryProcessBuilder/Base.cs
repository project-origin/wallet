using System;
using System.Collections.Generic;
using MassTransit;
using MassTransit.Courier.Contracts;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.WalletSystem.Server.Activities;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server;

public partial class RegistryProcessBuilder : IRegistryProcessBuilder
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEndpointNameFormatter _formatter;
    private readonly Guid _routingSlipId;
    private readonly string _owner;
    private readonly IRoutingSlipBuilder _slipBuilder;

    public RegistryProcessBuilder(IUnitOfWork unitOfWork, IEndpointNameFormatter formatter, Guid routingSlipId, string owner)
    {
        _unitOfWork = unitOfWork;
        _formatter = formatter;
        _routingSlipId = routingSlipId;
        _owner = owner;
        _slipBuilder = new RoutingSlipBuilder(routingSlipId);
    }

    internal void AddActivity<T, TArguments>(TArguments arguments)
        where T : class, IExecuteActivity<TArguments>
        where TArguments : class
    {
        var uri = new Uri($"exchange:{_formatter.ExecuteActivity<T, TArguments>()}");
        _slipBuilder.AddActivity(typeof(T).Name, uri, arguments);
    }

    public RoutingSlip Build()
    {
        return _slipBuilder.Build();
    }

    private void AddRegistryTransactionActivity(Transaction transaction, Guid sliceId)
    {
        AddActivity<SendRegistryTransactionActivity, SendRegistryTransactionArguments>(
        new SendRegistryTransactionArguments()
        {
            Transaction = transaction
        });

        AddActivity<WaitCommittedRegistryTransactionActivity, WaitCommittedTransactionArguments>(new WaitCommittedTransactionArguments()
        {
            RegistryName = transaction.Header.FederatedStreamId.Registry,
            TransactionId = transaction.ToShaId(),
            RequestId = _routingSlipId,
            Owner = _owner,
            CertificateId = new Guid(transaction.Header.FederatedStreamId.StreamId.Value),
            SliceId = sliceId
        });
    }

    public void SetWalletSliceStates(Dictionary<Guid, WalletSliceState> newStates)
    {
        AddActivity<UpdateSliceStateActivity, UpdateSliceStateArguments>(
            new UpdateSliceStateArguments
            {
                SliceStates = newStates
            });
    }
}
