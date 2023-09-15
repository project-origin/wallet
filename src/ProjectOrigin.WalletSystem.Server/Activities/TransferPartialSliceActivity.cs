using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Protobuf;
using MassTransit;
using Microsoft.Extensions.Logging;
using ProjectOrigin.Common.V1;
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
    public required Guid ReceiverDepositEndpointId { get; init; }
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
        _logger.LogTrace("RoutingSlip {TrackingNumber} - Executing {ActivityName}", context.TrackingNumber, context.ActivityName);

        try
        {
            var quantity = context.Arguments.Quantity;
            var sourceSlice = await _unitOfWork.CertificateRepository.GetSlice(context.Arguments.SourceSliceId);
            var receiverDepositEndpoint = await _unitOfWork.WalletRepository.GetDepositEndpoint(context.Arguments.ReceiverDepositEndpointId);

            var nextReceiverPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(receiverDepositEndpoint.Id);
            var receiverPublicKey = receiverDepositEndpoint.PublicKey.Derive(nextReceiverPosition).GetPublicKey();

            var sourceDepositEndpoint = await _unitOfWork.WalletRepository.GetDepositEndpoint(sourceSlice.DepositEndpointId);

            DepositEndpoint remainderDepositEndpoint = await _unitOfWork.WalletRepository.GetWalletRemainderDepositEndpoint(sourceDepositEndpoint.WalletId!.Value);
            var nextRemainderPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(remainderDepositEndpoint.Id); ;
            var remainderPublicKey = remainderDepositEndpoint.PublicKey.Derive(nextReceiverPosition).GetPublicKey();

            var remainder = (uint)sourceSlice.Quantity - quantity;

            var commitmentQuantity = new SecretCommitmentInfo(quantity);
            var commitmentRemainder = new SecretCommitmentInfo(remainder);

            var transferredSlice = new Slice
            {
                Id = Guid.NewGuid(),
                DepositEndpointId = receiverDepositEndpoint.Id,
                DepositEndpointPosition = nextReceiverPosition,
                Registry = sourceSlice.Registry,
                CertificateId = sourceSlice.CertificateId,
                Quantity = commitmentQuantity.Message,
                RandomR = commitmentQuantity.BlindingValue.ToArray(),
                SliceState = SliceState.Registering
            };
            await _unitOfWork.CertificateRepository.InsertSlice(transferredSlice);
            var remainderSlice = new Slice
            {
                Id = Guid.NewGuid(),
                DepositEndpointId = remainderDepositEndpoint.Id,
                DepositEndpointPosition = nextRemainderPosition,
                Registry = sourceSlice.Registry,
                CertificateId = sourceSlice.CertificateId,
                Quantity = commitmentRemainder.Message,
                RandomR = commitmentRemainder.BlindingValue.ToArray(),
                SliceState = SliceState.Registering
            };
            await _unitOfWork.CertificateRepository.InsertSlice(remainderSlice);

            var slicedEvent = CreateSliceEvent(sourceSlice, new NewSlice(commitmentQuantity, receiverPublicKey), new NewSlice(commitmentRemainder, remainderPublicKey));
            var sourceSlicePrivateKey = await _unitOfWork.WalletRepository.GetPrivateKeyForSlice(sourceSlice.Id);
            Transaction transaction = CreateAndSignTransaction(slicedEvent.CertificateId, slicedEvent, sourceSlicePrivateKey);

            _unitOfWork.Commit();

            var states = new Dictionary<Guid, SliceState>() {
                { sourceSlice.Id, SliceState.Sliced },
                { transferredSlice.Id, SliceState.Transferred },
                { remainderSlice.Id, SliceState.Available }
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

    private ExecutionResult AddTransferRequiredActivities(ExecuteContext context, DepositEndpoint receiverDepositEndpoint, Slice transferredSlice, Transaction transaction, Dictionary<Guid, SliceState> states)
    {
        return context.ReviseItinerary(builder =>
        {
            builder.AddActivity<SendRegistryTransactionActivity, SendTransactionArguments>(_formatter,
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

    private record NewSlice(SecretCommitmentInfo ci, IPublicKey Key);

    private SlicedEvent CreateSliceEvent(Slice sourceSlice, params NewSlice[] newSlices)
    {
        if (newSlices.Sum(s => s.ci.Message) != sourceSlice.Quantity)
            throw new InvalidOperationException();

        var certificateId = new ProjectOrigin.Common.V1.FederatedStreamId
        {
            Registry = sourceSlice.Registry,
            StreamId = new ProjectOrigin.Common.V1.Uuid { Value = sourceSlice.CertificateId.ToString() }
        };

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

    private static Transaction CreateAndSignTransaction(FederatedStreamId certificateId, IMessage @event, IHDPrivateKey slicePrivateKey)
    {
        var header = new TransactionHeader
        {
            FederatedStreamId = certificateId,
            PayloadType = @event.Descriptor.FullName,
            PayloadSha512 = ByteString.CopyFrom(SHA512.HashData(@event.ToByteArray())),
            Nonce = Guid.NewGuid().ToString(),
        };

        var transaction = new Transaction
        {
            Header = header,
            HeaderSignature = ByteString.CopyFrom(slicePrivateKey.Sign(header.ToByteArray())),
            Payload = @event.ToByteString()
        };

        return transaction;
    }
}
