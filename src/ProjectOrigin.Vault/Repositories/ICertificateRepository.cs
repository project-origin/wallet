using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.ViewModels;

namespace ProjectOrigin.Vault.Repositories;

public interface ICertificateRepository
{
    Task InsertCertificate(Certificate certificate);
    Task<Certificate?> GetCertificate(string registryName, Guid certificateId);

    Task<CertificateViewModel?> QueryCertificate(string owner, string registry, Guid certificateId);
    Task<PageResultCursor<CertificateViewModel>> QueryCertificates(QueryCertificatesFilterCursor filter);

    Task<PageResult<CertificateViewModel>> QueryAvailableCertificates(QueryCertificatesFilter filter);
    Task<PageResult<AggregatedCertificatesViewModel>> QueryAggregatedAvailableCertificates(QueryAggregatedCertificatesFilter filter);

    Task InsertWalletSlice(WalletSlice newSlice);
    Task<WalletSlice> GetWalletSlice(Guid sliceId);
    Task<IEnumerable<WalletSlice>> GetOwnersAvailableSlices(string registryName, Guid certificateId, string owner);
    Task<IList<WalletSlice>> ReserveQuantity(string owner, string registryName, Guid certificateId, uint reserveQuantity);
    Task<long> GetRegisteringAndAvailableQuantity(string registryName, Guid certificateId, string owner);
    Task SetWalletSliceState(Guid sliceId, WalletSliceState state);

    Task InsertWalletAttribute(Guid walletId, WalletAttribute walletAttribute);
    Task<IEnumerable<WalletAttribute>> GetWalletAttributes(Guid walletId, Guid certificateId, string registryName, IEnumerable<string> keys);
    Task WithdrawCertificate(string registry, Guid certificateId);
    Task<IEnumerable<WalletSlice>> GetClaimedSlicesOfCertificate(string registry, Guid certificateId);
    Task ExpireSlices(DateTimeOffset olderThanDate);
}
