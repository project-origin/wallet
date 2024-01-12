using System;
using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.ViewModels;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public interface IClaimRepository
{
    Task<Claim> GetClaim(Guid claimId);
    Task InsertClaim(Claim newClaim);
    Task SetClaimState(Guid claimId, ClaimState state);

    Task<PageResult<ClaimViewModel>> QueryClaims(ClaimFilter filter);
    Task<PageResult<AggregatedClaimViewModel>> QueryAggregatedClaims(ClaimFilter filter, TimeAggregate timeAggregate, string timeZone);
}
