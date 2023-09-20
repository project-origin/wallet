using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Protobuf;
using ProjectOrigin.Common.V1;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.WalletSystem.Server.Activities;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server;

public partial class RegistryProcessBuilder
{
    public async Task Claim(Slice prodSlice, Slice consSlice)
    {
        if (prodSlice.Quantity != consSlice.Quantity)
            throw new InvalidOperationException("Production and consumption slices must have the same quantity");

        var allocationId = Guid.NewGuid();

        var productionKey = await _unitOfWork.WalletRepository.GetPrivateKeyForSlice(prodSlice.Id);
        var consumptionKey = await _unitOfWork.WalletRepository.GetPrivateKeyForSlice(prodSlice.Id);

        var prodId = prodSlice.GetFederatedStreamId();
        var consId = consSlice.GetFederatedStreamId();

        var allocatedEvent = CreateAllocatedEvent(allocationId, consSlice, prodSlice);
        AddTransactionActivity(productionKey.SignTransaction(prodId, allocatedEvent));
        AddTransactionActivity(consumptionKey.SignTransaction(consId, allocatedEvent));

        var newClaim = new Claim
        {
            Id = allocationId,
            ProductionSliceId = prodSlice.Id,
            ConsumptionSliceId = consSlice.Id,
            State = ClaimState.Created,
        };
        await _unitOfWork.CertificateRepository.InsertClaim(newClaim);

        var prodClaimedEvent = CreateClaimedEvent(allocationId, prodId);
        AddTransactionActivity(productionKey.SignTransaction(prodId, prodClaimedEvent));

        var consClaimedEvent = CreateClaimedEvent(allocationId, consId);
        AddTransactionActivity(consumptionKey.SignTransaction(consId, consClaimedEvent));

        AddActivity<UpdateSliceStateActivity, UpdateSliceStateArguments>(new UpdateSliceStateArguments
        {
            SliceStates = new(){
                {prodSlice.Id, SliceState.Claimed},
                {consSlice.Id, SliceState.Claimed},
            }
        });

        AddActivity<UpdateClaimStateActivity, UpdateClaimStateArguments>(new UpdateClaimStateArguments
        {
            Id = allocationId,
            State = ClaimState.Claimed,
        });
    }

    private static AllocatedEvent CreateAllocatedEvent(Guid allocationId, Slice consumption, Slice production)
    {
        var cons = new SecretCommitmentInfo((uint)consumption.Quantity, consumption.RandomR);
        var prod = new SecretCommitmentInfo((uint)production.Quantity, production.RandomR);
        var equalityProof = SecretCommitmentInfo.CreateEqualityProof(prod, cons, allocationId.ToString());

        return new AllocatedEvent
        {
            AllocationId = new Uuid { Value = allocationId.ToString() },
            ProductionCertificateId = production.GetFederatedStreamId(),
            ConsumptionCertificateId = consumption.GetFederatedStreamId(),
            ProductionSourceSliceHash = ByteString.CopyFrom(SHA256.HashData(prod.Commitment.C)),
            ConsumptionSourceSliceHash = ByteString.CopyFrom(SHA256.HashData(cons.Commitment.C)),
            EqualityProof = ByteString.CopyFrom(equalityProof),
        };
    }

    private static ClaimedEvent CreateClaimedEvent(Guid allocationId, FederatedStreamId federatedStreamId)
    {
        return new ClaimedEvent
        {
            CertificateId = federatedStreamId,
            AllocationId = new Uuid { Value = allocationId.ToString() },
        };
    }
}
