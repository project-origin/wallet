using System;
using System.Threading.Tasks;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.ViewModels;

namespace ProjectOrigin.Vault.Repositories;

public interface IClaimRepository
{
    Task<Claim> GetClaim(Guid claimId);
    Task InsertClaim(Claim newClaim);
    Task SetClaimState(Guid claimId, ClaimState state);

    Task<PageResult<ClaimViewModel>> QueryClaims(QueryClaimsFilter filter);
    Task<PageResultCursor<ClaimViewModel>> QueryClaimsCursor(QueryClaimsFilterCursor filter);
    Task<PageResult<AggregatedClaimViewModel>> QueryAggregatedClaims(QueryAggregatedClaimsFilter filter);
}
