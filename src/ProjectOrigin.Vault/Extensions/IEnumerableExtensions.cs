using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProjectOrigin.Vault.Models;

namespace ProjectOrigin.Vault.Extensions;

public static class IEnumerableExtensions
{
    public static bool IsEmpty<TSource>(this IEnumerable<TSource> source) => !source.Any();

    public static IEnumerable<IGrouping<string, TSource>> GroupByTime<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, DateTimeOffset> timeSelector,
        TimeAggregate aggregation,
        TimeZoneInfo timeZone)
    {
        Func<TSource, DateTimeOffset> timeSelectWithTimezone = x => timeSelector(x).ToOffset(timeZone.GetUtcOffset(timeSelector(x).UtcDateTime));
        Func<TSource, string, string> selectToIso = (x, format) => timeSelectWithTimezone(x).ToString(format);

        var groupedMeasurements = aggregation switch
        {
            TimeAggregate.Total => source.GroupBy(x => "total"),
            TimeAggregate.Year => source.GroupBy(x => selectToIso(x, "yyyy")),
            TimeAggregate.Month => source.GroupBy(x => selectToIso(x, "yyyy-MM")),
            TimeAggregate.Week => source.GroupBy(x =>
            {
                var date = timeSelectWithTimezone(x).DateTime;
                var year = date.Year;
                var weekOfYear = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                return $"{year}-{weekOfYear:D2}";
            }),
            TimeAggregate.Day => source.GroupBy(x => selectToIso(x, "yyyy-MM-dd")),
            TimeAggregate.Hour => source.GroupBy(x => selectToIso(x, "yyyy-MM-ddTHH")),
            TimeAggregate.QuarterHour => source.GroupBy(x =>
            {
                var datetime = timeSelectWithTimezone(x).DateTime;
                var rounded = new DateTime(datetime.Year, datetime.Month, datetime.Day, datetime.Hour, datetime.Minute / 15 * 15, 0, datetime.Kind);
                return rounded.ToString("yyyy-MM-ddTHH:mm");
            }),
            TimeAggregate.Actual => source.GroupBy(x => selectToIso(x, "yyyy-MM-ddTHH:mm:ss")),
            _ => throw new NotImplementedException(),
        };
        return groupedMeasurements;
    }
}
