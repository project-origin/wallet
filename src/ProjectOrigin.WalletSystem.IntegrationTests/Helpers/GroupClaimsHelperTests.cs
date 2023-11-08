using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using ProjectOrigin.WalletSystem.Server.Helpers;
using ProjectOrigin.WalletSystem.Server.Models;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.Helpers;

public class GroupClaimsHelperTests
{
    [Fact]
    public void GroupsCorrectly()
    {
        var hoursIn30Days = 24 * 30;
        var claims = GenerateClaims(hoursIn30Days, new DateTimeOffset(2023, 11, 7, 0, 0,0, TimeSpan.Zero));

        var aggregated = GroupClaimsHelper.GroupByDay(claims).ToList();

        aggregated.Count.Should().Be(30);
    }

    private static List<ClaimViewModel> GenerateClaims(int numberOfClaims, DateTimeOffset startDate)
    {
        var claims = new List<ClaimViewModel>();
        for (int i = 0; i < numberOfClaims; i++)
        {
            var claim = new ClaimViewModel
            {
                ProductionStart = startDate.AddHours(i),
                ProductionEnd = startDate.AddHours(i + 1),
                Quantity = (uint)(42 + i),
                ConsumptionStart = startDate.AddHours(i),
                ConsumptionEnd = startDate.AddHours(i + 1),
                ConsumptionCertificateId = Guid.NewGuid(),
                ConsumptionGridArea = "DK1",
                ConsumptionRegistryName = "SomeRegistry",
                Id = Guid.NewGuid(),
                ProductionCertificateId = Guid.NewGuid(),
                ProductionGridArea = "DK1",
                ProductionRegistryName = "SomeRegistry"
            };
            claims.Add(claim);
        }

        return claims;
    } 
}
