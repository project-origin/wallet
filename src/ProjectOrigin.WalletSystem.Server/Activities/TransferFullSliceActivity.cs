using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Protobuf;
using MassTransit;
using Microsoft.Extensions.Logging;
using ProjectOrigin.Common.V1;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Activities;

public record TransferFullSliceArguments(Guid SourceSliceId, Guid ReceiverDepositEndpointId);

public class TransferFullSliceActivity : IExecuteActivity<TransferFullSliceArguments>
{
    private readonly UnitOfWork _unitOfWork;
    private readonly ILogger<TransferPartialSliceActivity> _logger;
    private readonly IEndpointNameFormatter _formatter;

    public TransferFullSliceActivity(
        UnitOfWork unitOfWork,
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
            var sourceSlice = await _unitOfWork.CertificateRepository.GetSlice(context.Arguments.SourceSliceId);
            var receiverDepositEndpoint = await _unitOfWork.WalletRepository.GetDepositEndpoint(context.Arguments.ReceiverDepositEndpointId);

            var nextReceiverPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(receiverDepositEndpoint.Id);
            var receiverPublicKey = receiverDepositEndpoint.PublicKey.Derive(nextReceiverPosition).GetPublicKey();

            var transferredSlice = new Slice(Guid.NewGuid(), receiverDepositEndpoint.Id, nextReceiverPosition, sourceSlice.RegistryId, sourceSlice.CertificateId, sourceSlice.Quantity, sourceSlice.RandomR, SliceState.Registering);
            await _unitOfWork.CertificateRepository.InsertSlice(transferredSlice);

            var registry = await _unitOfWork.RegistryRepository.GetRegistryFromId(sourceSlice.RegistryId);

            var transferredEvent = CreateTransferEvent(registry.Name, sourceSlice, receiverPublicKey);

            var sourceSlicePrivateKey = await _unitOfWork.WalletRepository.GetPrivateKeyForSlice(sourceSlice.Id);
            var transaction = CreateAndSignTransaction(transferredEvent.CertificateId, transferredEvent, sourceSlicePrivateKey);

            _unitOfWork.Commit();

            var states = new Dictionary<Guid, SliceState>() {
                { sourceSlice.Id, SliceState.Sliced },
                { transferredSlice.Id, SliceState.Transferred }
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

    private TransferredEvent CreateTransferEvent(string registryName, Slice sourceSlice, IPublicKey receiverPublicKey)
    {
        var sliceCommitment = new PedersenCommitment.SecretCommitmentInfo((uint)sourceSlice.Quantity, sourceSlice.RandomR);

        var certificateId = new FederatedStreamId
        {
            Registry = registryName,
            StreamId = new Uuid { Value = sourceSlice.CertificateId.ToString() }
        };

        var transferredEvent = new TransferredEvent
        {
            CertificateId = certificateId,
            NewOwner = new PublicKey
            {
                Content = ByteString.CopyFrom(receiverPublicKey.Export()),
                Type = KeyType.Secp256K1
            },
            SourceSliceHash = ByteString.CopyFrom(SHA256.HashData(sliceCommitment.Commitment.C))
        };
        return transferredEvent;
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
