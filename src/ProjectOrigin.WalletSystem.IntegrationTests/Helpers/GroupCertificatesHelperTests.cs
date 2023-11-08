using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using ProjectOrigin.WalletSystem.Server.Helpers;
using ProjectOrigin.WalletSystem.Server.Models;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.Helpers;

public class GroupCertificatesHelperTests
{
    [Fact]
    public void GroupsCorrectly()
    {
        var hoursIn30Days = 24 * 30;
        var certificates = GenerateCertificates(hoursIn30Days, new DateTimeOffset(2023, 11, 7, 0, 0,0, TimeSpan.Zero));

        var aggregated = GroupCertificatesHelper.GroupByDay(certificates).ToList();

        aggregated.Count.Should().Be(30);
    }

    private static List<CertificateViewModel> GenerateCertificates(int numberOfClaims, DateTimeOffset startDate)
    {
        var certificates = new List<CertificateViewModel>();
        for (int i = 0; i < numberOfClaims; i++)
        {
            var certificate = new CertificateViewModel
            {
                StartDate = startDate.AddHours(i),
                EndDate = startDate.AddHours(i + 1),
                CertificateType = i % 2 == 0 ? GranularCertificateType.Consumption : GranularCertificateType.Production,
                GridArea = "DK1",
                Id = Guid.NewGuid(),
                RegistryName = "SomeRegistry",
                Slices =
                {
                    new SliceViewModel
                    {
                        Quantity = 42 + i,
                        SliceId = Guid.NewGuid()
                    },
                    new SliceViewModel
                    {
                        Quantity = 42 + i,
                        SliceId = Guid.NewGuid()
                    }
                }
            };
            certificates.Add(certificate);
        }

        return certificates;
    } 
}
