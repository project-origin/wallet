using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Services;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public interface ICertificateRepository
{
    Task InsertReceivedSlice(ReceivedSlice newSlice);
    Task InsertDepositSlice(DepositSlice newSlice);

    Task InsertCertificate(Certificate certificate);
    Task<Certificate?> GetCertificate(string registryName, Guid certificateId);
    Task<IEnumerable<CertificateViewModel>> GetAllOwnedCertificates(string owner);
    Task<IEnumerable<ReceivedSlice>> GetOwnersAvailableSlices(string registryName, Guid certificateId, string owner);
    Task<IList<ReceivedSlice>> ReserveQuantity(string owner, string registryName, Guid certificateId, uint reserveQuantity);

    Task<ReceivedSlice> GetReceivedSlice(Guid sliceId);
    Task<DepositSlice> GetDepositSlice(Guid sliceId);

    Task SetReceivedSliceState(Guid sliceId, ReceivedSliceState state);
    Task SetDepositSliceState(Guid sliceId, DepositSliceState state);

    Task InsertClaim(Claim newClaim);
    Task SetClaimState(Guid claimId, ClaimState state);
    Task<Claim> GetClaim(Guid claimId);

    Task<IEnumerable<ClaimViewModel>> GetClaims(string owner, ClaimFilter claimFilter);
}
