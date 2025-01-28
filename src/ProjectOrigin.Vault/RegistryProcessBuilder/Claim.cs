using System;
using System.Threading.Tasks;
using ProjectOrigin.Common.V1;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.Vault.Activities;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Models;

namespace ProjectOrigin.Vault;

public partial class RegistryProcessBuilder
{
    public async Task Claim(WalletSlice productionSlice, WalletSlice consumptionSlice)
    {
        if (productionSlice.Quantity != consumptionSlice.Quantity)
            throw new InvalidOperationException("Production and consumption slices must have the same quantity");

        var allocationId = _routingSlipId; //_routingSlipId = ClaimId

        AddActivity<AllocateActivity, AllocateArguments>(new AllocateArguments
        {
            AllocationId = allocationId,
            CertificateId = productionSlice.GetFederatedStreamId(),
            ProductionSliceId = productionSlice.Id,
            ConsumptionSliceId = consumptionSlice.Id,
            ChroniclerRequestId = await GetClaimIntentId(productionSlice),
            RequestStatusArgs = new RequestStatusArgs
            {
                Owner = _owner,
                RequestId = _routingSlipId
            }
        });

        AddActivity<AllocateActivity, AllocateArguments>(new AllocateArguments
        {
            AllocationId = allocationId,
            CertificateId = consumptionSlice.GetFederatedStreamId(),
            ProductionSliceId = productionSlice.Id,
            ConsumptionSliceId = consumptionSlice.Id,
            ChroniclerRequestId = await GetClaimIntentId(consumptionSlice),
            RequestStatusArgs = new RequestStatusArgs
            {
                Owner = _owner,
                RequestId = _routingSlipId
            }
        });

        await _unitOfWork.ClaimRepository.InsertClaim(new Claim
        {
            Id = allocationId,
            ProductionSliceId = productionSlice.Id,
            ConsumptionSliceId = consumptionSlice.Id,
            State = ClaimState.Created,
        });

        var productionKey = await _unitOfWork.WalletRepository.GetPrivateKeyForSlice(productionSlice.Id);
        var consumptionKey = await _unitOfWork.WalletRepository.GetPrivateKeyForSlice(consumptionSlice.Id);

        var productionId = productionSlice.GetFederatedStreamId();
        var consumptionId = consumptionSlice.GetFederatedStreamId();

        var prodClaimedEvent = CreateClaimedEvent(allocationId, productionSlice.GetFederatedStreamId());
        AddRegistryTransactionActivity(productionKey.SignRegistryTransaction(productionId, prodClaimedEvent), productionSlice.Id);

        var consClaimedEvent = CreateClaimedEvent(allocationId, consumptionSlice.GetFederatedStreamId());
        AddRegistryTransactionActivity(consumptionKey.SignRegistryTransaction(consumptionId, consClaimedEvent), consumptionSlice.Id);

        AddActivity<UpdateSliceStateActivity, UpdateSliceStateArguments>(new UpdateSliceStateArguments
        {
            SliceStates = new(){
                {productionSlice.Id, WalletSliceState.Claimed},
                {consumptionSlice.Id, WalletSliceState.Claimed},
            }
        });

        AddActivity<UpdateClaimStateActivity, UpdateClaimStateArguments>(new UpdateClaimStateArguments
        {
            Id = allocationId,
            State = ClaimState.Claimed,
            RequestStatusArgs = new RequestStatusArgs
            {
                RequestId = _routingSlipId,
                Owner = _owner
            }
        });
    }

    private async Task<Guid?> GetClaimIntentId(WalletSlice slice)
    {
        var certificate = await _unitOfWork.CertificateRepository.GetCertificate(slice.RegistryName, slice.CertificateId)
            ?? throw new InvalidOperationException($"Certificate not found {slice.RegistryName}-{slice.CertificateId}");

        if (!_networkOptions.Value.Areas.TryGetValue(certificate.GridArea, out var areaInfo))
            throw new InvalidOperationException($"Area not found {certificate.GridArea}");

        Guid? chroniclerRequestId = null;
        if (areaInfo.Chronicler is not null)
        {
            chroniclerRequestId = Guid.NewGuid();
            AddActivity<SendClaimIntentToChroniclerActivity, SendClaimIntentToChroniclerArguments>(new SendClaimIntentToChroniclerArguments
            {
                Id = chroniclerRequestId.Value,
                CertificateId = slice.GetFederatedStreamId(),
                Quantity = (int)slice.Quantity,
                RandomR = slice.RandomR
            });
        }

        return chroniclerRequestId;
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
