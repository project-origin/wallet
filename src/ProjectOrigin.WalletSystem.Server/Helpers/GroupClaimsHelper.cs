using System;
using System.Collections.Generic;
using System.Linq;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Services.REST.v1;

namespace ProjectOrigin.WalletSystem.Server.Helpers;

public static class GroupClaimsHelper
{
    public static IEnumerable<AggregationResult> GroupByDay(IEnumerable<ClaimViewModel> claims)
    {
        return claims
            .GroupBy(x => new DateTimeOffset(x.ProductionStart.Date))
            .Select(x => new AggregationResult
            {
                Quantity = x.Sum(y => y.Quantity),
                Start = x.Key.ToUnixTimeSeconds(),
                End = x.Key.AddDays(1).ToUnixTimeSeconds()
            });
    }
}
