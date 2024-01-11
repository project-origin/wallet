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

    Task<PageResult<ClaimViewModel>> QueryClaims(ClaimFilter claimFilter);
    Task<PageResult<AggregatedClaimViewModel>> QueryAggregatedClaims(ClaimFilter claimFilter, TimeAggregate timeAggregate, string timeZone);
}
