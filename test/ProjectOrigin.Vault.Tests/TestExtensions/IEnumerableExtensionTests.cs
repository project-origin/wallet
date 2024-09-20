using System;
using System.Collections.Generic;
using FluentAssertions;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Models;
using Xunit;

namespace ProjectOrigin.Vault.Tests.TestExtensions;

public class IEnumerableExtensionTests
{
    [Theory]
    [InlineData("Europe/Copenhagen", TimeAggregate.Actual, 14400)]
    [InlineData("Europe/Copenhagen", TimeAggregate.Total, 1)]
    [InlineData("Europe/Copenhagen", TimeAggregate.Year, 1)]
    [InlineData("America/Toronto", TimeAggregate.Year, 2)]
    [InlineData("Europe/Copenhagen", TimeAggregate.Month, 1)]
    [InlineData("America/Toronto", TimeAggregate.Month, 2)]
    [InlineData("Europe/Copenhagen", TimeAggregate.Week, 3)]
    [InlineData("Europe/Copenhagen", TimeAggregate.Day, 11)]
    [InlineData("Europe/London", TimeAggregate.Day, 10)]
    [InlineData("America/Toronto", TimeAggregate.Day, 11)]
    [InlineData("Europe/Copenhagen", TimeAggregate.Hour, 240)]
    [InlineData("Europe/London", TimeAggregate.Hour, 240)]
    [InlineData("America/Toronto", TimeAggregate.Hour, 240)]
    [InlineData("Europe/Copenhagen", TimeAggregate.QuarterHour, 960)]
    [InlineData("Europe/London", TimeAggregate.QuarterHour, 960)]
    [InlineData("America/Toronto", TimeAggregate.QuarterHour, 960)]
    public void TestGroupByTime(string timeZone, TimeAggregate timeAggregate, int expectedCount)
    {
        // Arrange
        var certs = new List<TestObject>();
        for (int i = 0; i < TimeSpan.FromDays(10).TotalMinutes; i++)
        {
            var date = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(i);
            certs.Add(new TestObject { Start = date });
            certs.Add(new TestObject { Start = date });
        }

        // Act
        var groupedCerts = certs.GroupByTime(x => x.Start, timeAggregate, TimeZoneInfo.FindSystemTimeZoneById(timeZone));

        // Assert
        groupedCerts.Should().HaveCount(expectedCount);
    }

    private sealed record TestObject
    {
        public required DateTimeOffset Start { get; init; }
    }
}
