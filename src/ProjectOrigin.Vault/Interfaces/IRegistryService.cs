using System;
using System.Threading.Tasks;
using ProjectOrigin.Vault.Projections;

namespace ProjectOrigin.Vault.Services;

public interface IRegistryService
{
    Task<GetCertificateResult> GetGranularCertificate(string registryName, Guid certificateId);
}

public abstract record GetCertificateResult()
{
    public sealed record Success(GranularCertificate GranularCertificate) : GetCertificateResult;
    public sealed record NotFound() : GetCertificateResult;
    public sealed record Failure(Exception Exception) : GetCertificateResult;
    public sealed record TransientFailure(Exception Exception) : GetCertificateResult;
}
