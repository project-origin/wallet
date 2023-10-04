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
    public required Guid ExternalEndpointsId { get; init; }
    public required uint Quantity { get; init; }
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
            var quantity = context.Arguments.Quantity;
            var sourceSlice = await _unitOfWork.CertificateRepository.GetWalletSlice(context.Arguments.SourceSliceId);
            var externalEndpoints = await _unitOfWork.WalletRepository.GetExternalEndpoints(context.Arguments.ExternalEndpointsId);

            var nextReceiverPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(externalEndpoints.Id);
            var receiverPublicKey = externalEndpoints.PublicKey.Derive(nextReceiverPosition).GetPublicKey();

            var sourceEndpoint = await _unitOfWork.WalletRepository.GetWalletEndpoint(sourceSlice.WalletEndpointId);

            var remainderEndpoint = await _unitOfWork.WalletRepository.GetWalletRemainderEndpoint(sourceEndpoint.WalletId);
            var nextRemainderPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(remainderEndpoint.Id);
            var remainderPublicKey = remainderEndpoint.PublicKey.Derive(nextReceiverPosition).GetPublicKey();

            var remainder = (uint)sourceSlice.Quantity - quantity;

            var commitmentQuantity = new SecretCommitmentInfo(quantity);
            var commitmentRemainder = new SecretCommitmentInfo(remainder);

            var transferredSlice = new TransferredSlice
            {
                Id = Guid.NewGuid(),
                ExternalEndpointsId = externalEndpoints.Id,
                ExternalEndpointsPosition = nextReceiverPosition,
                RegistryName = sourceSlice.RegistryName,
                CertificateId = sourceSlice.CertificateId,
                Quantity = commitmentQuantity.Message,
                RandomR = commitmentQuantity.BlindingValue.ToArray(),
                SliceState = TransferredSliceState.Registering
            };
            await _unitOfWork.CertificateRepository.InsertTransferredSlice(transferredSlice);
            var remainderSlice = new WalletSlice
            {
                Id = Guid.NewGuid(),
                WalletEndpointId = remainderEndpoint.Id,
                WalletEndpointPosition = nextRemainderPosition,
                RegistryName = sourceSlice.RegistryName,
                CertificateId = sourceSlice.CertificateId,
                Quantity = commitmentRemainder.Message,
                RandomR = commitmentRemainder.BlindingValue.ToArray(),
                SliceState = WalletSliceState.Registering
            };
            await _unitOfWork.CertificateRepository.InsertWalletSlice(remainderSlice);

            var slicedEvent = CreateSliceEvent(sourceSlice, new NewSlice(commitmentQuantity, receiverPublicKey), new NewSlice(commitmentRemainder, remainderPublicKey));
            var sourceSlicePrivateKey = await _unitOfWork.WalletRepository.GetPrivateKeyForSlice(sourceSlice.Id);
            Transaction transaction = sourceSlicePrivateKey.SignRegistryTransaction(slicedEvent.CertificateId, slicedEvent);

            _unitOfWork.Commit();

            var states = new Dictionary<Guid, WalletSliceState>() {
                { sourceSlice.Id, WalletSliceState.Sliced },
                { remainderSlice.Id, WalletSliceState.Available }
            };

            return AddTransferRequiredActivities(context, externalEndpoints, transferredSlice, transaction, states);
        }
        catch (Exception ex)
        {
            _unitOfWork.Rollback();
            _logger.LogError(ex, "Error sending transactions to registry");
            return context.Faulted(ex);
        }
    }

    private ExecutionResult AddTransferRequiredActivities(ExecuteContext context, ExternalEndpoints externalEndpoints, BaseSlice transferredSlice, Transaction transaction, Dictionary<Guid, WalletSliceState> states)
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
                    ExternalEndpointsId = externalEndpoints.Id,
                    SliceId = transferredSlice.Id,
                });

            builder.AddActivitiesFromSourceItinerary();
        });
    }

    private sealed record NewSlice(SecretCommitmentInfo ci, IPublicKey Key);

    private SlicedEvent CreateSliceEvent(WalletSlice sourceSlice, params NewSlice[] newSlices)
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
