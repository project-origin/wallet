using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.ViewModels;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public interface ICertificateRepository
{
    Task InsertCertificate(Certificate certificate);
    Task<Certificate?> GetCertificate(string registryName, Guid certificateId);

    Task<PageResult<CertificateViewModel>> QueryAvailableCertificates(CertificatesFilter filter);
    Task<PageResult<AggregatedCertificatesViewModel>> QueryAvailableCertificatesAggregated(CertificatesFilter filter, TimeAggregate timeAggregate, string timeZone);

    Task InsertWalletSlice(WalletSlice newSlice);
    Task<WalletSlice> GetWalletSlice(Guid sliceId);
    Task<IEnumerable<WalletSlice>> GetOwnersAvailableSlices(string registryName, Guid certificateId, string owner);
    Task<IList<WalletSlice>> ReserveQuantity(string owner, string registryName, Guid certificateId, uint reserveQuantity);
    Task SetWalletSliceState(Guid sliceId, WalletSliceState state);

    Task<IEnumerable<TransferViewModel>> GetTransfers(string owner, TransferFilter filter);
    Task InsertTransferredSlice(TransferredSlice newSlice);
    Task<TransferredSlice> GetTransferredSlice(Guid sliceId);
    Task SetTransferredSliceState(Guid sliceId, TransferredSliceState state);

    Task InsertClaim(Claim newClaim);
    Task SetClaimState(Guid claimId, ClaimState state);
    Task<Claim> GetClaim(Guid claimId);
    Task<IEnumerable<ClaimViewModel>> GetClaims(string owner, ClaimFilter claimFilter);

    Task InsertWalletAttribute(Guid walletId, WalletAttribute walletAttribute);
    Task<IEnumerable<WalletAttribute>> GetWalletAttributes(Guid walletId, Guid certificateId, string registryName, IEnumerable<string> keys);

}
