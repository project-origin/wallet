using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public interface ICertificateRepository
{
    Task InsertSlice(Slice newSlice);
    Task InsertCertificate(Certificate certificate);
    Task<Certificate?> GetCertificate(Guid registryId, Guid certificateId);
    Task<IEnumerable<CertificateViewModel>> GetAllOwnedCertificates(string owner);
    Task<IEnumerable<Slice>> GetOwnerAvailableSlices(string registryName, Guid certificateId, string owner);
    Task<IEnumerable<Slice>> GetToBeAvailable(string registryName, Guid certificateId, string owner);
    Task<Slice> GetSlice(Guid sliceId);
    Task SetSliceState(Guid sliceId, SliceState state);
}
