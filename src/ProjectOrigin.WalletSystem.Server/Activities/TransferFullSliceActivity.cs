using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Protobuf;
using MassTransit;
using Microsoft.Extensions.Logging;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Activities;

public record TransferFullSliceArguments
{
    public required Guid SourceSliceId { get; init; }
    public required Guid ReceiverDepositEndpointId { get; init; }
}

public class TransferFullSliceActivity : IExecuteActivity<TransferFullSliceArguments>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransferPartialSliceActivity> _logger;
    private readonly IEndpointNameFormatter _formatter;

    public TransferFullSliceActivity(
        IUnitOfWork unitOfWork,
        ILogger<TransferPartialSliceActivity> logger,
        IEndpointNameFormatter formatter)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _formatter = formatter;
    }


    public async Task<ExecutionResult> Execute(ExecuteContext<TransferFullSliceArguments> context)
    {
        _logger.LogTrace("RoutingSlip {TrackingNumber} - Executing {ActivityName}", context.TrackingNumber, context.ActivityName);

        try
        {
            var sourceSlice = await _unitOfWork.CertificateRepository.GetReceivedSlice(context.Arguments.SourceSliceId);
            var receiverDepositEndpoint = await _unitOfWork.WalletRepository.GetDepositEndpoint(context.Arguments.ReceiverDepositEndpointId);

            var nextReceiverPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(receiverDepositEndpoint.Id);
            var receiverPublicKey = receiverDepositEndpoint.PublicKey.Derive(nextReceiverPosition).GetPublicKey();

            var transferredSlice = new DepositSlice
            {
                Id = Guid.NewGuid(),
                DepositEndpointId = receiverDepositEndpoint.Id,
                DepositEndpointPosition = nextReceiverPosition,
                RegistryName = sourceSlice.RegistryName,
                CertificateId = sourceSlice.CertificateId,
                Quantity = sourceSlice.Quantity,
                RandomR = sourceSlice.RandomR,
                SliceState = DepositSliceState.Registering
            };
            await _unitOfWork.CertificateRepository.InsertDepositSlice(transferredSlice);


            var transferredEvent = CreateTransferEvent(sourceSlice, receiverPublicKey);

            var sourceSlicePrivateKey = await _unitOfWork.WalletRepository.GetPrivateKeyForSlice(sourceSlice.Id);
            var transaction = sourceSlicePrivateKey.SignRegistryTransaction(transferredEvent.CertificateId, transferredEvent);

            _unitOfWork.Commit();

            var states = new Dictionary<Guid, ReceivedSliceState>() {
                { sourceSlice.Id, ReceivedSliceState.Sliced }
            };

            return AddTransferRequiredActivities(context, receiverDepositEndpoint, transferredSlice, transaction, states);
        }
        catch (Exception ex)
        {
            _unitOfWork.Rollback();
            _logger.LogError(ex, "Error sending transactions to registry");
            return context.Faulted(ex);
        }
    }

    private ExecutionResult AddTransferRequiredActivities(ExecuteContext context, DepositEndpoint receiverDepositEndpoint, BaseSlice transferredSlice, Transaction transaction, Dictionary<Guid, ReceivedSliceState> states)
    {
        return context.ReviseItinerary(builder =>
        {
            builder.AddActivity<SendRegistryTransactionActivity, SendRegistryTransactionArguments>(_formatter,
                new()
                {
                    Transaction = transaction
                });

            builder.AddActivity<WaitCommittedRegistryTransactionActivity, WaitCommittedTransactionArguments>(_formatter,
                new()
                {
                    RegistryName = transaction.Header.FederatedStreamId.Registry,
                    TransactionId = transaction.ToShaId()
                });

            builder.AddActivity<UpdateSliceStateActivity, UpdateSliceStateArguments>(_formatter,
                new()
                {
                    SliceStates = states
                });

            builder.AddActivity<SendInformationToReceiverWalletActivity, SendInformationToReceiverWalletArgument>(_formatter,
                new()
                {
                    ReceiverDepositEndpointId = receiverDepositEndpoint.Id,
                    SliceId = transferredSlice.Id,
                });

            builder.AddActivitiesFromSourceItinerary();
        });
    }

    private static TransferredEvent CreateTransferEvent(ReceivedSlice sourceSlice, IPublicKey receiverPublicKey)
    {
        var sliceCommitment = new PedersenCommitment.SecretCommitmentInfo((uint)sourceSlice.Quantity, sourceSlice.RandomR);

        var transferredEvent = new TransferredEvent
        {
            CertificateId = sourceSlice.GetFederatedStreamId(),
            NewOwner = new PublicKey
            {
                Content = ByteString.CopyFrom(receiverPublicKey.Export()),
                Type = KeyType.Secp256K1
            },
            SourceSliceHash = ByteString.CopyFrom(SHA256.HashData(sliceCommitment.Commitment.C))
        };
        return transferredEvent;
    }
}
