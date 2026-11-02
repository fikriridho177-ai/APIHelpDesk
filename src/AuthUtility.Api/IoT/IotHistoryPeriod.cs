using System.Globalization;

namespace AuthUtility.Api.IoT;

public sealed class IotHistoryPeriodDefinition
{
    private IotHistoryPeriodDefinition(
        string code,
        string label,
        string aggregationCode)
    {
        Code = code;
        Label = label;
        AggregationCode = aggregationCode;
    }

    public string Code { get; }
    public string Label { get; }
    public string AggregationCode { get; }

    public static bool TryCreate(
        string? value,
        out IotHistoryPeriodDefinition definition)
    {
        definition = value?.Trim().ToLowerInvariant() switch
        {
            "day" => new("day", "Harian · interval logger", "sample"),
            "week" => new("week", "Mingguan · 7 hari", "day"),
            "month" => new("month", "Bulanan · 30 hari", "day"),
            "year" => new("year", "Tahunan · 12 bulan", "month"),
            _ => null!
        };

        return definition is not null;
    }

    public static bool TryParseAnchorDate(
        string? value,
        out DateOnly anchorDate)
    {
        return DateOnly.TryParseExact(
            value?.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out anchorDate);
    }

    public static bool TryParseRangeDateTime(
        string? value,
        out DateTime dateTime)
    {
        return DateTime.TryParseExact(
            value?.Trim(),
            "yyyy-MM-dd'T'HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out dateTime);
    }

    public IotHistoryWindow Resolve(
        DateTime now,
        DateOnly? anchorDate = null,
        DateTime? rangeFrom = null,
        DateTime? rangeTo = null)
    {
        if (rangeFrom.HasValue || rangeTo.HasValue)
        {
            if (!rangeFrom.HasValue ||
                !rangeTo.HasValue ||
                rangeFrom.Value >= rangeTo.Value)
            {
                throw new ArgumentException(
                    "Rentang history IoT harus memiliki from < to.");
            }

            return new IotHistoryWindow(
                Code,
                AggregationCode,
                rangeFrom.Value,
                rangeTo.Value);
        }

        var date = anchorDate ?? DateOnly.FromDateTime(now);
        var fromDate = Code switch
        {
            "day" => date,
            "week" => date.AddDays(-6),
            "month" => date.AddDays(-29),
            "year" => new DateOnly(date.Year, date.Month, 1).AddMonths(-11),
            _ => throw new InvalidOperationException(
                $"Periode IoT tidak didukung: {Code}")
        };

        var toDate = Code == "year"
            ? new DateOnly(date.Year, date.Month, 1).AddMonths(1)
            : date.AddDays(1);

        return new IotHistoryWindow(
            Code,
            AggregationCode,
            fromDate.ToDateTime(TimeOnly.MinValue),
            toDate.ToDateTime(TimeOnly.MinValue));
    }

    public DateTime GetBucketStart(DateTime timestamp)
    {
        return AggregationCode switch
        {
            "hour" => new DateTime(
                timestamp.Year,
                timestamp.Month,
                timestamp.Day,
                timestamp.Hour,
                0,
                0),
            "day" => timestamp.Date,
            "month" => new DateTime(
                timestamp.Year,
                timestamp.Month,
                1),
            _ => throw new InvalidOperationException(
                $"Agregasi IoT tidak didukung: {AggregationCode}")
        };
    }
}

public sealed record IotHistoryWindow(
    string PeriodCode,
    string AggregationCode,
    DateTime From,
    DateTime To);

public sealed record IotHistoryPoint(
    long RawId,
    int LoggerId,
    DateTime? Ts,
    DateTime? ReceivedAt,
    IReadOnlyDictionary<string, decimal?> Data);

public sealed record IotHistoryAggregationMode(string Code)
{
    public static readonly IotHistoryAggregationMode Average = new("average");
    public static readonly IotHistoryAggregationMode Sum = new("sum");

    public static bool TryParse(
        string? value,
        out IotHistoryAggregationMode mode)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(value.Trim(), "average", StringComparison.OrdinalIgnoreCase))
        {
            mode = Average;
            return true;
        }

        if (string.Equals(value.Trim(), "sum", StringComparison.OrdinalIgnoreCase))
        {
            mode = Sum;
            return true;
        }

        mode = Average;
        return false;
    }
}

public static class IotHistoryAggregator
{
    public static IReadOnlyList<IotHistoryPoint> Aggregate(
        IEnumerable<IotHistoryPoint> points,
        IotHistoryPeriodDefinition period,
        IotHistoryAggregationMode? aggregationMode = null)
    {
        var mode = aggregationMode ?? IotHistoryAggregationMode.Average;

        if (period.AggregationCode == "sample")
        {
            return points
                .Where(point => point.Ts.HasValue)
                .OrderBy(point => point.Ts)
                .ThenBy(point => point.RawId)
                .ToArray();
        }

        return points
            .Where(point => point.Ts.HasValue)
            .GroupBy(point => period.GetBucketStart(point.Ts!.Value))
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var latest = group
                    .OrderByDescending(point => point.Ts)
                    .ThenByDescending(point => point.RawId)
                    .First();

                var keys = group
                    .SelectMany(point => point.Data.Keys)
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                var data = new Dictionary<string, decimal?>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (var key in keys)
                {
                    var values = group
                        .Select(point => point.Data
                            .FirstOrDefault(entry =>
                                string.Equals(
                                    entry.Key,
                                    key,
                                    StringComparison.OrdinalIgnoreCase))
                            .Value)
                        .Where(value => value.HasValue)
                        .Select(value => value!.Value)
                        .ToArray();

                    data[key] = values.Length == 0
                        ? null
                        : mode == IotHistoryAggregationMode.Sum
                            ? values.Sum()
                            : values.Average();
                }

                return new IotHistoryPoint(
                    latest.RawId,
                    latest.LoggerId,
                    group.Key,
                    latest.ReceivedAt,
                    data);
            })
            .ToArray();
    }
}
