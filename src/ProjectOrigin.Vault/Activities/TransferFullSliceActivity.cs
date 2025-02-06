using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Protobuf;
using MassTransit;
using Microsoft.Extensions.Logging;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Models;

namespace ProjectOrigin.Vault.Activities;

public record TransferFullSliceArguments
{
    public required Guid SourceSliceId { get; init; }
    public required Guid ExternalEndpointId { get; init; }
    public required string[] HashedAttributes { get; init; }
    public required RequestStatusArgs RequestStatusArgs { get; init; }
}

public class TransferFullSliceActivity : IExecuteActivity<TransferFullSliceArguments>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransferFullSliceActivity> _logger;
    private readonly IEndpointNameFormatter _formatter;

    public TransferFullSliceActivity(
        IUnitOfWork unitOfWork,
        ILogger<TransferFullSliceActivity> logger,
        IEndpointNameFormatter formatter)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _formatter = formatter;
    }

    public async Task<ExecutionResult> Execute(ExecuteContext<TransferFullSliceArguments> context)
    {
        _logger.LogDebug("RoutingSlip {TrackingNumber} - Executing {ActivityName}", context.TrackingNumber, context.ActivityName);
        _logger.LogInformation("Starting Activity: {Activity}, RequestId: {RequestId} ", nameof(TransferFullSliceArguments), context.Arguments.RequestStatusArgs.RequestId);

        try
        {
            var sourceSlice = await _unitOfWork.CertificateRepository.GetWalletSlice(context.Arguments.SourceSliceId);
            var sourceEndpoint = await _unitOfWork.WalletRepository.GetWalletEndpoint(sourceSlice.WalletEndpointId);
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
            await _unitOfWork.TransferRepository.InsertTransferredSlice(transferredSlice);

            _logger.LogInformation($"Registering transfer for certificateId {sourceSlice.CertificateId}");

            var transferredEvent = CreateTransferEvent(sourceSlice, receiverPublicKey);

            var sourceSlicePrivateKey = await _unitOfWork.WalletRepository.GetPrivateKeyForSlice(sourceSlice.Id);
            var transaction = sourceSlicePrivateKey.SignRegistryTransaction(transferredEvent.CertificateId, transferredEvent);
            var walletAttributes = await _unitOfWork.CertificateRepository.GetWalletAttributes(sourceEndpoint.WalletId, sourceSlice.CertificateId, sourceSlice.RegistryName, context.Arguments.HashedAttributes);

            _unitOfWork.Commit();

            var states = new Dictionary<Guid, WalletSliceState>() {
                { sourceSlice.Id, WalletSliceState.Sliced }
            };
            _logger.LogInformation("Ending Activity: {Activity}, RequestId: {RequestId} ", nameof(TransferFullSliceArguments), context.Arguments.RequestStatusArgs.RequestId);


            return AddTransferRequiredActivities(context, externalEndpoint, transferredSlice, transaction, states, walletAttributes);
        }
        catch (Exception ex)
        {
            _unitOfWork.Rollback();
            _logger.LogError(ex, "Error sending transactions to registry");
            return context.Faulted(ex);
        }
    }

    private ExecutionResult AddTransferRequiredActivities(ExecuteContext<TransferFullSliceArguments> context, ExternalEndpoint externalEndpoint, AbstractSlice transferredSlice, Transaction transaction, Dictionary<Guid, WalletSliceState> states, IEnumerable<WalletAttribute> walletAttributes)
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
                    TransactionId = transaction.ToShaId(),
                    CertificateId = transferredSlice.CertificateId,
                    SliceId = transferredSlice.Id,
                    RequestStatusArgs = context.Arguments.RequestStatusArgs
                });

            builder.AddActivity<UpdateSliceStateActivity, UpdateSliceStateArguments>(_formatter,
                new()
                {
                    SliceStates = states,
                    RequestStatusArgs = context.Arguments.RequestStatusArgs
                });

            builder.AddActivity<SendInformationToReceiverWalletActivity, SendInformationToReceiverWalletArgument>(_formatter,
                new()
                {
                    ExternalEndpointId = externalEndpoint.Id,
                    SliceId = transferredSlice.Id,
                    WalletAttributes = walletAttributes.ToArray(),
                    RequestStatusArgs = context.Arguments.RequestStatusArgs
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
