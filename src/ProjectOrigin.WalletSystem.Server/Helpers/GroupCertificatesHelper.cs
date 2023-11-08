using System;
using System.Collections.Generic;
using System.Linq;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Services.REST.v1;

namespace ProjectOrigin.WalletSystem.Server.Helpers
{
    public static class GroupCertificatesHelper
    {
        public static IEnumerable<AggregationResult> GroupByDay(IEnumerable<CertificateViewModel> certificates)
        {
            return certificates
                .GroupBy(x => new DateTimeOffset(x.StartDate.Date))
                .Select(x => new AggregationResult
                {
                    Quantity = x.Sum(y => y.Slices.Sum(z => z.Quantity)),
                    Start = x.Key.ToUnixTimeSeconds(),
                    End = x.Key.AddDays(1).ToUnixTimeSeconds()
                });
        }
    }
}
