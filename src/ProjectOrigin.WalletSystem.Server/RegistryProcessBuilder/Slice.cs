using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Protobuf;
using MassTransit;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.WalletSystem.Server.Activities;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server;

public partial class RegistryProcessBuilder
{
    public async Task<(Slice, Slice)> SplitSlice(Slice source, long quantity)
    {
        if (source.Quantity <= quantity)
            throw new InvalidOperationException("Cannot split slice with quantity less than or equal to the requested quantity");

        var sliceDepositEndpoint = await _unitOfWork.WalletRepository.GetDepositEndpoint(source.DepositEndpointId);
        var remainderEndpoint = await _unitOfWork.WalletRepository.GetWalletRemainderDepositEndpoint(sliceDepositEndpoint.WalletId ??
            throw new InvalidOperationException("Deposit endpoint must have a wallet id"));

        Slice claimSlice = await CreateAndInsertSlice(source, remainderEndpoint, (uint)quantity);
        Slice remainderSlice = await CreateAndInsertSlice(source, remainderEndpoint, (uint)(source.Quantity - quantity));
        await _unitOfWork.CertificateRepository.SetSliceState(source.Id, SliceState.Slicing);

        var privateKey = await _unitOfWork.WalletRepository.GetPrivateKeyForSlice(source.Id);

        BuildSliceRoutingSlip(remainderEndpoint, source, privateKey, claimSlice, remainderSlice);

        return (claimSlice, remainderSlice);
    }

    private async Task<Slice> CreateAndInsertSlice(Slice source, DepositEndpoint remainderEndpoint, uint quantity)
    {
        var newSecretCommitmentInfo = new SecretCommitmentInfo(quantity);
        var newSlice = source with
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = remainderEndpoint.Id,
            DepositEndpointPosition = await _unitOfWork.WalletRepository.GetNextNumberForId(remainderEndpoint.Id),
            Quantity = newSecretCommitmentInfo.Message,
            RandomR = newSecretCommitmentInfo.BlindingValue.ToArray(),
            SliceState = SliceState.Registering
        };

        await _unitOfWork.CertificateRepository.InsertSlice(newSlice);
        return newSlice;
    }

    private void BuildSliceRoutingSlip(DepositEndpoint remainderEndpoint, Slice sourceSlice, IHDPrivateKey privateKey, params Slice[] newSlices)
    {
        var mappedSlices = newSlices.Select(s =>
        {
            var commitmentInfo = new SecretCommitmentInfo((uint)s.Quantity, s.RandomR);
            var publicKey = remainderEndpoint.PublicKey.Derive(s.DepositEndpointPosition).GetPublicKey();
            return new NewSlice(commitmentInfo, publicKey);
        }).ToArray();

        var sliceEvent = CreateSliceEvent(sourceSlice, mappedSlices);
        var transaction = privateKey.SignRegistryTransaction(sliceEvent.CertificateId, sliceEvent);

        AddRegistryTransactionActivity(transaction);
        AddActivity<UpdateSliceStateActivity, UpdateSliceStateArguments>(new UpdateSliceStateArguments
        {
            SliceStates = newSlices
                .Select(s => KeyValuePair.Create(s.Id, SliceState.Reserved))
                .Append(KeyValuePair.Create(sourceSlice.Id, SliceState.Sliced))
                .ToDictionary(x => x.Key, x => x.Value)
        });
    }

    private sealed record NewSlice(SecretCommitmentInfo ci, IPublicKey Key);

    private static SlicedEvent CreateSliceEvent(Slice sourceSlice, params NewSlice[] newSlices)
    {
        if (newSlices.Sum(s => s.ci.Message) != sourceSlice.Quantity)
            throw new InvalidOperationException("Sum of new slices must be equal to the source slice quantity");

        var certificateId = sourceSlice.GetFederatedStreamId();

        var sourceSliceCommitment = new SecretCommitmentInfo((uint)sourceSlice.Quantity, sourceSlice.RandomR);
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
                Quantity = new Electricity.V1.Commitment
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
