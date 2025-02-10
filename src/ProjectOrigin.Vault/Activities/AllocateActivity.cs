using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Protobuf;
using MassTransit;
using Microsoft.Extensions.Logging;
using Npgsql;
using ProjectOrigin.Common.V1;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Exceptions;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Models;

namespace ProjectOrigin.Vault.Activities;

public record AllocateArguments
{
    public required Guid AllocationId { get; init; }
    public required Guid ConsumptionSliceId { get; init; }
    public required Guid ProductionSliceId { get; init; }
    public Guid? ChroniclerRequestId { get; init; }
    public required FederatedStreamId CertificateId { get; init; }
    public required RequestStatusArgs RequestStatusArgs { get; init; }
}

public class AllocateActivity : IExecuteActivity<AllocateArguments>
{
    private readonly ILogger<AllocateActivity> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEndpointNameFormatter _formatter;
    private readonly IClaimMetrics _claimMetrics;

    public AllocateActivity(
        ILogger<AllocateActivity> logger,
        IUnitOfWork unitOfWork,
        IEndpointNameFormatter formatter,
        IClaimMetrics claimMetrics)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _formatter = formatter;
        _claimMetrics = claimMetrics;
    }

    public async Task<ExecutionResult> Execute(ExecuteContext<AllocateArguments> context)
    {
        try
        {
            _logger.LogInformation("Starting Activity: {Activity}, RequestId: {RequestId} ", nameof(AllocateActivity), context.Arguments.RequestStatusArgs.RequestId);
            var cons = await _unitOfWork.CertificateRepository.GetWalletSlice(context.Arguments.ConsumptionSliceId);
            var prod = await _unitOfWork.CertificateRepository.GetWalletSlice(context.Arguments.ProductionSliceId);

            byte[]? chroniclerSignature = context.Arguments.ChroniclerRequestId is not null
                ? Convert.FromBase64String(context.GetVariable<string>(context.Arguments.ChroniclerRequestId.Value.ToString())
                    ?? throw new InvalidOperationException("Allocate activity with ChroniclerRequestId but result variable not found"))
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
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "Failed to communicate with the database.");
            throw new TransientException("Failed to communicate with the database.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error allocating certificate");
            _unitOfWork.Rollback();
            await _unitOfWork.RequestStatusRepository.SetRequestStatus(context.Arguments.RequestStatusArgs.RequestId,
                context.Arguments.RequestStatusArgs.Owner,
                RequestStatusState.Failed,
                "Error allocating certificate");
            _unitOfWork.Commit();
            _claimMetrics.IncrementFailedClaims();
            throw;
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
