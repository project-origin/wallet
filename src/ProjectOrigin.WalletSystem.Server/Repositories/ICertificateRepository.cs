using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public interface ICertificateRepository
{

    Task InsertCertificate(Certificate certificate);
    Task<Certificate?> GetCertificate(string registryName, Guid certificateId);
    Task<IEnumerable<CertificateViewModel>> GetAllOwnedCertificates(string owner);

    Task InsertWalletSlice(WalletSlice newSlice);
    Task<WalletSlice> GetWalletSlice(Guid sliceId);
    Task<IEnumerable<WalletSlice>> GetOwnersAvailableSlices(string registryName, Guid certificateId, string owner);
    Task<IList<WalletSlice>> ReserveQuantity(string owner, string registryName, Guid certificateId, uint reserveQuantity);
    Task SetWalletSliceState(Guid sliceId, WalletSliceState state);

    Task InsertOutboxSlice(OutboxSlice newSlice);
    Task<OutboxSlice> GetOutboxSlice(Guid sliceId);
    Task SetOutboxSliceState(Guid sliceId, OutboxSliceState state);

    Task InsertClaim(Claim newClaim);
    Task SetClaimState(Guid claimId, ClaimState state);
    Task<Claim> GetClaim(Guid claimId);
    Task<IEnumerable<ClaimViewModel>> GetClaims(string owner, ClaimFilter claimFilter);
}
