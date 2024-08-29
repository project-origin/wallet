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
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Activities;

public record TransferPartialSliceArguments
{
    public required Guid SourceSliceId { get; init; }
    public required Guid ExternalEndpointId { get; init; }
    public required uint Quantity { get; init; }
    public required string[] HashedAttributes { get; init; }
    public required Guid RequestId { get; init; }
    public required string Owner { get; init; }
}

public class TransferPartialSliceActivity : IExecuteActivity<TransferPartialSliceArguments>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransferPartialSliceActivity> _logger;
    private readonly IEndpointNameFormatter _formatter;

    public TransferPartialSliceActivity(
        IUnitOfWork unitOfWork,
        ILogger<TransferPartialSliceActivity> logger,
        IEndpointNameFormatter formatter)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _formatter = formatter;
    }

    public async Task<ExecutionResult> Execute(ExecuteContext<TransferPartialSliceArguments> context)
    {
        _logger.LogDebug("RoutingSlip {TrackingNumber} - Executing {ActivityName}", context.TrackingNumber, context.ActivityName);

        try
        {
            var sourceSlice = await _unitOfWork.CertificateRepository.GetWalletSlice(context.Arguments.SourceSliceId);
            var sourceEndpoint = await _unitOfWork.WalletRepository.GetWalletEndpoint(sourceSlice.WalletEndpointId);

            var quantity = context.Arguments.Quantity;
            var remainder = (uint)sourceSlice.Quantity - quantity;

            var receiverEndpoints = await _unitOfWork.WalletRepository.GetExternalEndpoint(context.Arguments.ExternalEndpointId);
            var receiverPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(receiverEndpoints.Id);
            var receiverPublicKey = receiverEndpoints.PublicKey.Derive(receiverPosition).GetPublicKey();
            var receiverCommitment = new SecretCommitmentInfo(quantity);
            var transferredSlice = new TransferredSlice
            {
                Id = Guid.NewGuid(),
                ExternalEndpointId = receiverEndpoints.Id,
                ExternalEndpointPosition = receiverPosition,
                RegistryName = sourceSlice.RegistryName,
                CertificateId = sourceSlice.CertificateId,
                Quantity = receiverCommitment.Message,
                RandomR = receiverCommitment.BlindingValue.ToArray(),
                State = TransferredSliceState.Registering
            };
            await _unitOfWork.TransferRepository.InsertTransferredSlice(transferredSlice);

            var remainderEndpoint = await _unitOfWork.WalletRepository.GetWalletRemainderEndpoint(sourceEndpoint.WalletId);
            var remainderPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(remainderEndpoint.Id);
            var remainderPublicKey = remainderEndpoint.PublicKey.Derive(remainderPosition).GetPublicKey();
            var remainderCommitment = new SecretCommitmentInfo(remainder);
            var remainderSlice = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = remainderEndpoint.Id,
                WalletEndpointPosition = remainderPosition,
                RegistryName = sourceSlice.RegistryName,
                CertificateId = sourceSlice.CertificateId,
                Quantity = remainderCommitment.Message,
                RandomR = remainderCommitment.BlindingValue.ToArray(),
                State = WalletSliceState.Registering
            };
            await _unitOfWork.CertificateRepository.InsertWalletSlice(remainderSlice);

            var slicedEvent = CreateSliceEvent(sourceSlice, new NewSlice(receiverCommitment, receiverPublicKey), new NewSlice(remainderCommitment, remainderPublicKey));
            var sourceSlicePrivateKey = await _unitOfWork.WalletRepository.GetPrivateKeyForSlice(sourceSlice.Id);
            var transaction = sourceSlicePrivateKey.SignRegistryTransaction(slicedEvent.CertificateId, slicedEvent);
            var walletAttributes = await _unitOfWork.CertificateRepository.GetWalletAttributes(sourceEndpoint.WalletId, sourceSlice.CertificateId, sourceSlice.RegistryName, context.Arguments.HashedAttributes);

            _unitOfWork.Commit();

            var states = new Dictionary<Guid, WalletSliceState>() {
                { sourceSlice.Id, WalletSliceState.Sliced },
                { remainderSlice.Id, WalletSliceState.Available }
            };

            return AddTransferRequiredActivities(context, receiverEndpoints, transferredSlice, transaction, states, walletAttributes);
        }
        catch (Exception ex)
        {
            _unitOfWork.Rollback();
            _logger.LogError(ex, "Error sending transactions to registry");
            return context.Faulted(ex);
        }
    }

    private ExecutionResult AddTransferRequiredActivities(ExecuteContext<TransferPartialSliceArguments> context, ExternalEndpoint externalEndpoint, AbstractSlice transferredSlice, Transaction transaction, Dictionary<Guid, WalletSliceState> states, IEnumerable<WalletAttribute> walletAttributes)
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
                    RequestId = context.Arguments.RequestId,
                    Owner = context.Arguments.Owner,
                    CertificateId = transferredSlice.CertificateId,
                    SliceId = transferredSlice.Id
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
                    WalletAttributes = walletAttributes.ToArray(),
                    RequestId = context.Arguments.RequestId,
                    Owner = context.Arguments.Owner
                });

            builder.AddActivitiesFromSourceItinerary();
        });
    }

    private sealed record NewSlice(SecretCommitmentInfo ci, IPublicKey Key);

    private static SlicedEvent CreateSliceEvent(WalletSlice sourceSlice, params NewSlice[] newSlices)
    {
        if (newSlices.Sum(s => s.ci.Message) != sourceSlice.Quantity)
            throw new InvalidOperationException();

        var certificateId = sourceSlice.GetFederatedStreamId();

        var sourceSliceCommitment = new PedersenCommitment.SecretCommitmentInfo((uint)sourceSlice.Quantity, sourceSlice.RandomR);
        var sumOfNewSlices = newSlices.Select(newSlice => newSlice.ci).Aggregate((left, right) => left + right);
        var equalityProof = SecretCommitmentInfo.CreateEqualityProof(sourceSliceCommitment, sumOfNewSlices, certificateId.StreamId.Value);

        var slicedEvent = new SlicedEvent
        {
            CertificateId = certificateId,
            SumProof = ByteString.CopyFrom(equalityProof),
            SourceSliceHash = ByteString.CopyFrom(SHA256.HashData(sourceSliceCommitment.Commitment.C))
        };

        foreach (var newSlice in newSlices)
        {
            var poSlice = new SlicedEvent.Types.Slice
            {
                NewOwner = new PublicKey
                {
                    Type = KeyType.Secp256K1,
                    Content = ByteString.CopyFrom(newSlice.Key.Export())
                },
                Quantity = new ProjectOrigin.Electricity.V1.Commitment
                {
                    Content = ByteString.CopyFrom(newSlice.ci.Commitment.C),
                    RangeProof = ByteString.CopyFrom(newSlice.ci.CreateRangeProof(certificateId.StreamId.Value))
                }
            };
            slicedEvent.NewSlices.Add(poSlice);
        }

        return slicedEvent;
    }
}
