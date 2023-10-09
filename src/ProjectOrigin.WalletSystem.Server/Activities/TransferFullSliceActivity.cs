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
    public required Guid ExternalEndpointId { get; init; }
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
        _logger.LogDebug("RoutingSlip {TrackingNumber} - Executing {ActivityName}", context.TrackingNumber, context.ActivityName);

        try
        {
            var sourceSlice = await _unitOfWork.CertificateRepository.GetWalletSlice(context.Arguments.SourceSliceId);
            var externalEndpoint = await _unitOfWork.WalletRepository.GetExternalEndpoint(context.Arguments.ExternalEndpointId);

            var nextReceiverPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(externalEndpoint.Id);
            var receiverPublicKey = externalEndpoint.PublicKey.Derive(nextReceiverPosition).GetPublicKey();

            var transferredSlice = new TransferredSlice
            {
                Id = Guid.NewGuid(),
                ExternalEndpointId = externalEndpoint.Id,
                ExternalEndpointPosition = nextReceiverPosition,
                RegistryName = sourceSlice.RegistryName,
                CertificateId = sourceSlice.CertificateId,
                Quantity = sourceSlice.Quantity,
                RandomR = sourceSlice.RandomR,
                State = TransferredSliceState.Registering
            };
            await _unitOfWork.CertificateRepository.InsertTransferredSlice(transferredSlice);


            var transferredEvent = CreateTransferEvent(sourceSlice, receiverPublicKey);

            var sourceSlicePrivateKey = await _unitOfWork.WalletRepository.GetPrivateKeyForSlice(sourceSlice.Id);
            var transaction = sourceSlicePrivateKey.SignRegistryTransaction(transferredEvent.CertificateId, transferredEvent);

            _unitOfWork.Commit();

            var states = new Dictionary<Guid, WalletSliceState>() {
                { sourceSlice.Id, WalletSliceState.Sliced }
            };

            return AddTransferRequiredActivities(context, externalEndpoint, transferredSlice, transaction, states);
        }
        catch (Exception ex)
        {
            _unitOfWork.Rollback();
            _logger.LogError(ex, "Error sending transactions to registry");
            return context.Faulted(ex);
        }
    }

    private ExecutionResult AddTransferRequiredActivities(ExecuteContext context, ExternalEndpoint externalEndpoint, AbstractSlice transferredSlice, Transaction transaction, Dictionary<Guid, WalletSliceState> states)
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
                    ExternalEndpointId = externalEndpoint.Id,
                    SliceId = transferredSlice.Id,
                });

            builder.AddActivitiesFromSourceItinerary();
        });
    }

    private static TransferredEvent CreateTransferEvent(WalletSlice sourceSlice, IPublicKey receiverPublicKey)
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
