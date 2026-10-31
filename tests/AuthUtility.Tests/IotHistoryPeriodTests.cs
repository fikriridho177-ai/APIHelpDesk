using AuthUtility.Api.IoT;

namespace AuthUtility.Tests;

public sealed class IotHistoryPeriodTests
{
    [Theory]
    [InlineData("day", "sample")]
    [InlineData("week", "day")]
    [InlineData("month", "day")]
    [InlineData("year", "month")]
    public void TryCreate_returns_expected_aggregation(
        string code,
        string aggregation)
    {
        var created = IotHistoryPeriodDefinition.TryCreate(
            code,
            out var definition);

        Assert.True(created);
        Assert.Equal(code, definition.Code);
        Assert.Equal(aggregation, definition.AggregationCode);
    }

    [Fact]
    public void TryCreate_rejects_unknown_period()
    {
        var created = IotHistoryPeriodDefinition.TryCreate(
            "quarter",
            out _);

        Assert.False(created);
    }

    [Fact]
    public void Resolve_uses_calendar_anchor_for_each_period()
    {
        var now = new DateTime(2026, 10, 26, 9, 30, 0);
        var anchorDate = new DateOnly(2026, 10, 26);

        var day = IotHistoryPeriodDefinition.TryCreate("day", out var dayPeriod)
            ? dayPeriod
            : throw new InvalidOperationException();
        var dayWindow = day.Resolve(now, anchorDate);
        Assert.Equal(new DateTime(2026, 10, 26), dayWindow.From);
        Assert.Equal(new DateTime(2026, 10, 27), dayWindow.To);

        var week = IotHistoryPeriodDefinition.TryCreate("week", out var weekPeriod)
            ? weekPeriod
            : throw new InvalidOperationException();
        var weekWindow = week.Resolve(now, anchorDate);
        Assert.Equal(new DateTime(2026, 10, 20), weekWindow.From);
        Assert.Equal(new DateTime(2026, 10, 27), weekWindow.To);

        var month = IotHistoryPeriodDefinition.TryCreate("month", out var monthPeriod)
            ? monthPeriod
            : throw new InvalidOperationException();
        var monthWindow = month.Resolve(now, anchorDate);
        Assert.Equal(new DateTime(2026, 9, 27), monthWindow.From);
        Assert.Equal(new DateTime(2026, 10, 27), monthWindow.To);

        var year = IotHistoryPeriodDefinition.TryCreate("year", out var yearPeriod)
            ? yearPeriod
            : throw new InvalidOperationException();
        var yearWindow = year.Resolve(now, anchorDate);
        Assert.Equal(new DateTime(2025, 11, 1), yearWindow.From);
        Assert.Equal(new DateTime(2026, 11, 1), yearWindow.To);
    }

    [Fact]
    public void Resolve_defaults_to_current_calendar_date_when_anchor_is_missing()
    {
        var now = new DateTime(2026, 10, 26, 9, 30, 0);
        var definition = IotHistoryPeriodDefinition.TryCreate(
            "day",
            out var period)
            ? period
            : throw new InvalidOperationException();

        var window = definition.Resolve(now);

        Assert.Equal(new DateTime(2026, 10, 26), window.From);
        Assert.Equal(new DateTime(2026, 10, 27), window.To);
    }

    [Fact]
    public void Resolve_uses_explicit_range_when_provided()
    {
        var definition = IotHistoryPeriodDefinition.TryCreate(
            "week",
            out var period)
            ? period
            : throw new InvalidOperationException();
        var from = new DateTime(2026, 10, 20, 6, 15, 0);
        var to = new DateTime(2026, 10, 27, 18, 45, 0);

        var window = definition.Resolve(
            new DateTime(2026, 10, 26, 9, 30, 0),
            rangeFrom: from,
            rangeTo: to);

        Assert.Equal(from, window.From);
        Assert.Equal(to, window.To);
    }

    [Fact]
    public void TryParseAnchorDate_accepts_iso_calendar_date()
    {
        Assert.True(
            IotHistoryPeriodDefinition.TryParseAnchorDate(
                "2026-10-26",
                out var anchorDate));
        Assert.Equal(new DateOnly(2026, 10, 26), anchorDate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2026-02-30")]
    [InlineData("26-10-2026")]
    public void TryParseAnchorDate_rejects_non_iso_or_invalid_dates(string value)
    {
        Assert.False(IotHistoryPeriodDefinition.TryParseAnchorDate(value, out _));
    }

    [Fact]
    public void TryParseRangeDateTime_accepts_local_iso_date_time()
    {
        Assert.True(
            IotHistoryPeriodDefinition.TryParseRangeDateTime(
                "2026-10-26T14:30",
                out var value));
        Assert.Equal(new DateTime(2026, 10, 26, 14, 30, 0), value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2026-02-30T14:30")]
    [InlineData("2026-10-26 14:30")]
    public void TryParseRangeDateTime_rejects_non_iso_or_invalid_values(string value)
    {
        Assert.False(IotHistoryPeriodDefinition.TryParseRangeDateTime(value, out _));
    }

    [Fact]
    public void Aggregate_sample_preserves_each_logger_reading()
    {
        var definition = IotHistoryPeriodDefinition.TryCreate(
            "day",
            out var period)
            ? period
            : throw new InvalidOperationException();

        var points = new[]
        {
            new IotHistoryPoint(
                1,
                7,
                new DateTime(2026, 10, 26, 8, 10, 0),
                new DateTime(2026, 10, 26, 8, 11, 0),
                new Dictionary<string, decimal?> { ["TEMP"] = 20 }),
            new IotHistoryPoint(
                2,
                7,
                new DateTime(2026, 10, 26, 8, 40, 0),
                new DateTime(2026, 10, 26, 8, 41, 0),
                new Dictionary<string, decimal?> { ["TEMP"] = 30 })
        };

        var result = IotHistoryAggregator.Aggregate(points, period);

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateTime(2026, 10, 26, 8, 10, 0), result[0].Ts);
        Assert.Equal(20, result[0].Data["TEMP"]);
        Assert.Equal(new DateTime(2026, 10, 26, 8, 40, 0), result[1].Ts);
        Assert.Equal(30, result[1].Data["TEMP"]);
    }

    [Fact]
    public void Aggregate_averages_numeric_values_and_uses_bucket_start()
    {
        var definition = IotHistoryPeriodDefinition.TryCreate(
            "week",
            out var period)
            ? period
            : throw new InvalidOperationException();

        var points = new[]
        {
            new IotHistoryPoint(
                1,
                7,
                new DateTime(2026, 10, 26, 8, 10, 0),
                new DateTime(2026, 10, 26, 8, 11, 0),
                new Dictionary<string, decimal?>
                {
                    ["TEMP"] = 20,
                    ["RAIN"] = null
                }),
            new IotHistoryPoint(
                2,
                7,
                new DateTime(2026, 10, 26, 8, 40, 0),
                new DateTime(2026, 10, 26, 8, 41, 0),
                new Dictionary<string, decimal?>
                {
                    ["TEMP"] = 30,
                    ["RAIN"] = 5
                })
        };

        var result = IotHistoryAggregator.Aggregate(points, period);

        var bucket = Assert.Single(result);
        Assert.Equal(new DateTime(2026, 10, 26), bucket.Ts);
        Assert.Equal(25, bucket.Data["TEMP"]);
        Assert.Equal(5, bucket.Data["RAIN"]);
        Assert.Equal(2, bucket.RawId);
    }
}
