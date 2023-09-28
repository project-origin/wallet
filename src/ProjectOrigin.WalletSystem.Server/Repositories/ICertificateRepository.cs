using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public interface ICertificateRepository
{
    Task InsertSlice(Slice newSlice);
    Task InsertCertificate(Certificate certificate);
    Task<Certificate?> GetCertificate(string registryName, Guid certificateId);
    Task<IEnumerable<CertificateViewModel>> GetAllOwnedCertificates(string owner);
    Task<IEnumerable<Slice>> GetOwnersAvailableSlices(string registryName, Guid certificateId, string owner);
    Task<IList<Slice>> ReserveQuantity(string owner, string registryName, Guid certificateId, uint reserveQuantity);

    Task<Slice> GetSlice(Guid sliceId);
    Task SetSliceState(Guid sliceId, SliceState state);
    Task InsertClaim(Claim newClaim);
    Task SetClaimState(Guid claimId, ClaimState state);
    Task<Claim> GetClaim(Guid claimId);

    Task<ReceivedSlice?> GetTop1ReceivedSlice();
    Task RemoveReceivedSlice(ReceivedSlice receivedSlice);
}
