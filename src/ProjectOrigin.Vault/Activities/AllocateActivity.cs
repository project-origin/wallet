using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Protobuf;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Common.V1;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Options;

namespace ProjectOrigin.Vault.Activities;

public record AllocateArguments
{
    public required Guid AllocationId { get; init; }
    public required Guid ConsumptionSliceId { get; init; }
    public required Guid ProductionSliceId { get; init; }
    public Guid? ChroniclerRequestId { get; init; }
    public required FederatedStreamId CertificateId { get; init; }
    public required Guid RequestId { get; init; }
    public required string Owner { get; init; }
}

public class AllocateActivity : IExecuteActivity<AllocateArguments>
{
    private readonly ILogger<AllocateActivity> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEndpointNameFormatter _formatter;

    public AllocateActivity(
        ILogger<AllocateActivity> logger,
        IUnitOfWork unitOfWork,
        IEndpointNameFormatter formatter)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _formatter = formatter;
    }

    public async Task<ExecutionResult> Execute(ExecuteContext<AllocateArguments> context)
    {
        try
        {
            var cons = await _unitOfWork.CertificateRepository.GetWalletSlice(context.Arguments.ConsumptionSliceId);
            var prod = await _unitOfWork.CertificateRepository.GetWalletSlice(context.Arguments.ProductionSliceId);

            byte[]? chroniclerSignature = context.Arguments.ChroniclerRequestId is not null
                ? context.GetVariable<byte[]>(context.Arguments.ChroniclerRequestId.Value.ToString())
                : null;

            var allocatedEvent = CreateAllocatedEvent(context.Arguments.AllocationId, cons, prod, chroniclerSignature);

            var slice = cons.RegistryName == context.Arguments.CertificateId.Registry
                   && cons.CertificateId == Guid.Parse(context.Arguments.CertificateId.StreamId.Value)
                ? cons : prod;

            var key = await _unitOfWork.WalletRepository.GetPrivateKeyForSlice(slice.Id);

            var transaction = key.SignRegistryTransaction(context.Arguments.CertificateId, allocatedEvent);

            _logger.LogDebug("Claim intent registered with Chronicler");

            return AddTransferRequiredActivities(context, transaction, slice.Id);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering claim intent with Chronicler");
            return context.Faulted(ex);
        }
    }

    private ExecutionResult AddTransferRequiredActivities(ExecuteContext<AllocateArguments> context, Transaction transaction, Guid sliceId)
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
                    CertificateId = Guid.Parse(context.Arguments.CertificateId.StreamId.Value),
                    SliceId = sliceId
                });

            builder.AddActivitiesFromSourceItinerary();
        });
    }

    private static Electricity.V1.AllocatedEvent CreateAllocatedEvent(Guid allocationId, WalletSlice consumption, WalletSlice production, byte[]? chroniclerSignature)
    {
        var cons = new SecretCommitmentInfo((uint)consumption.Quantity, consumption.RandomR);
        var prod = new SecretCommitmentInfo((uint)production.Quantity, production.RandomR);
        var equalityProof = SecretCommitmentInfo.CreateEqualityProof(prod, cons, allocationId.ToString());

        return new Electricity.V1.AllocatedEvent
        {
            AllocationId = new Uuid { Value = allocationId.ToString() },
            ProductionCertificateId = production.GetFederatedStreamId(),
            ConsumptionCertificateId = consumption.GetFederatedStreamId(),
            ProductionSourceSliceHash = ByteString.CopyFrom(SHA256.HashData(prod.Commitment.C)),
            ConsumptionSourceSliceHash = ByteString.CopyFrom(SHA256.HashData(cons.Commitment.C)),
            EqualityProof = ByteString.CopyFrom(equalityProof),
            ChroniclerSignature = chroniclerSignature is null ? ByteString.Empty : ByteString.CopyFrom(chroniclerSignature)
        };
    }
}
