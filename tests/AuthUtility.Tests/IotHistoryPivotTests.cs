using System.Data;
using AuthUtility.Api.IoT;

namespace AuthUtility.Tests;

public sealed class IotHistoryPivotTests
{
    private static DataTable Metadata()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Code", typeof(string));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Unit", typeof(string));
        foreach (var id in Enumerable.Range(5, 10)) table.Rows.Add(id, $"P{id}", $"Parameter {id}", "unit");
        return table;
    }

    private static DataTable Values()
    {
        var table = new DataTable();
        table.Columns.Add("Timestamp", typeof(DateTime));
        table.Columns.Add("ParameterId", typeof(int));
        table.Columns.Add("RawId", typeof(long));
        table.Columns.Add("ReceivedAt", typeof(DateTime));
        table.Columns.Add("Value", typeof(decimal));
        return table;
    }

    private static async Task<IotHistoryPivot.Result> Read(DataTable values, long total, int loggerId = 2, int page = 1)
    {
        var count = new DataTable();
        count.Columns.Add("Total", typeof(long));
        count.Columns.Add("Snapshot", typeof(long));
        count.Rows.Add(total, 123L);
        using var reader = new DataTableReader(new[] { count, Metadata(), values });
        return await IotHistoryPivot.Read(reader, loggerId, page, 25, "sample", CancellationToken.None);
    }

    [Fact]
    public async Task TenParametersProduceOneRowWithoutLosingZeroNullOrPrecision()
    {
        var time = new DateTime(2026, 11, 16, 0, 5, 0);
        var values = Values();
        decimal?[] sample = [0m, 25.4m, 24.7m, 96m, 0m, null, 1010.8m, 0m, 346m, 1.2m];
        for (var i = 0; i < sample.Length; i++) values.Rows.Add(time, i + 5, 100L, time, (object?)sample[i] ?? DBNull.Value);
        var result = await Read(values, 1);
        var row = Assert.Single(result.Data);
        Assert.Equal(10, row.Data.Count);
        Assert.Equal("0", row.Data["5"]);
        Assert.Equal("25.4", row.Data["6"]);
        Assert.Equal("96", row.Data["8"]);
        Assert.Null(row.Data["10"]);
        Assert.Equal("1010.8", row.Data["11"]);
        Assert.Equal(new DateTime(2026, 11, 15, 17, 5, 0, DateTimeKind.Utc), row.Ts);
        Assert.Equal(DateTimeKind.Utc, row.Ts.Kind);
        Assert.Equal(Enumerable.Range(5, 10), result.Parameters.Select(x => x.SensorParameterId));
        Assert.All(result.Parameters, parameter => Assert.True(parameter.Active));
    }

    [Fact]
    public async Task DistinctSecondsAndSubMillisecondTimestampsRemainSeparate()
    {
        var time = new DateTime(2026, 11, 16, 0, 5, 0);
        var values = Values();
        foreach (var timestamp in new[] { time, time.AddTicks(1), time.AddSeconds(30), time.AddMinutes(5) })
            values.Rows.Add(timestamp, 5, 100L, timestamp, 0.000001m);
        var result = await Read(values, 4);
        Assert.Equal(4, result.Data.Count);
        Assert.Equal(4, result.Data.Select(x => x.Ts).Distinct().Count());
        Assert.All(result.Data, row => Assert.Equal("0.000001", row.Data["5"]));
    }

    [Fact]
    public async Task PaginationCountsTimestampGroupsAndKeepsAllParameters()
    {
        var time = new DateTime(2026, 11, 16, 0, 5, 0);
        var values = Values();
        foreach (var id in Enumerable.Range(5, 10)) values.Rows.Add(time, id, 100L, time, id);
        var result = await Read(values, 300, page: 2);
        Assert.Equal(300, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(123, result.SnapshotRawId);
        Assert.Equal(10, Assert.Single(result.Data).Data.Count);
    }

    [Fact]
    public async Task SameTimestampOnDifferentLoggersKeepsLoggerIdentity()
    {
        var values = Values();
        values.Rows.Add(new DateTime(2026, 11, 16), 5, 100L, new DateTime(2026, 11, 16), 0m);
        var first = await Read(values, 1, 1);
        var second = await Read(values, 1, 2);
        Assert.NotEqual(first.Data[0].LoggerId, second.Data[0].LoggerId);
        Assert.Equal(first.Data[0].Ts, second.Data[0].Ts);
    }

    [Fact]
    public void QueryPaginatesBucketsBeforeJoiningDetailsAndResolvesRevisionsDeterministically()
    {
        var sql = IotHistoryPivot.Sql(false);
        Assert.True(sql.IndexOf("OFFSET @offset", StringComparison.Ordinal) < sql.IndexOf("WITH Ranked", StringComparison.Ordinal));
        Assert.Contains("GROUP BY Bucket", sql);
        Assert.Contains("PARTITION BY H.LoggerID, H.TanggalSensor, D.SensorParameterID", sql);
        Assert.Contains("X.IsCurrent = 1", sql);
        Assert.Contains("R.ProcessedAt DESC, R.HasilID DESC, H.ReceivedAt DESC", sql);
        Assert.Contains("H.RawID DESC, D.RawDtlID DESC", sql);
        Assert.Contains("H.RawID <= @watermark", sql);
        Assert.Contains("R.StatusHasil = 'OK'", sql);
        Assert.Contains("DATEADD(day, -30, @now)", sql);
        Assert.Contains("GROUP BY D.SensorParameterID", sql);
        Assert.Contains("ORDER BY Bucket DESC", sql);
        Assert.Contains("ORDER BY Bucket ASC", IotHistoryPivot.Sql(true));
    }
}
