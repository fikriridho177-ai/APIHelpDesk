using System.Data.Common;
using System.Globalization;

namespace AuthUtility.Api.IoT;

public static class IotHistoryPivot
{
    public static DateTime ToUtc(DateTime wib) => new DateTimeOffset(
        DateTime.SpecifyKind(wib, DateTimeKind.Unspecified), TimeSpan.FromHours(7)).UtcDateTime;

    public static string Sql(bool ascending) => $$"""
        DECLARE @watermark bigint = COALESCE(@snapshotRawId,
            (SELECT MAX(RawID) FROM dbo.TrnLoggerRawHdr WHERE LoggerID = @loggerId));
        DECLARE @now datetime2 = DATEADD(hour, 7, SYSUTCDATETIME());
        -- Kolom hanya untuk sensor yang masih mengirim hasil valid (contoh AWLC tanpa suhu/kelembapan).
        DECLARE @activeSince datetime2 = DATEADD(day, -30, @now);
        SELECT H.RawID, H.LoggerID, H.TanggalSensor, H.ReceivedAt,
            CASE @aggregation
                WHEN 'hour' THEN DATETIME2FROMPARTS(YEAR(H.TanggalSensor), MONTH(H.TanggalSensor), DAY(H.TanggalSensor), DATEPART(hour, H.TanggalSensor), 0, 0, 0, 7)
                WHEN 'day' THEN CONVERT(datetime2(7), CONVERT(date, H.TanggalSensor))
                WHEN 'month' THEN DATETIME2FROMPARTS(YEAR(H.TanggalSensor), MONTH(H.TanggalSensor), 1, 0, 0, 0, 0, 7)
                ELSE H.TanggalSensor END AS Bucket
        INTO #HistoryHeaders
        FROM dbo.TrnLoggerRawHdr AS H
        WHERE H.LoggerID = @loggerId AND H.TanggalSensor >= @from AND H.TanggalSensor < @to
          AND H.RawID <= @watermark;

        SELECT Bucket INTO #HistoryTimes FROM #HistoryHeaders GROUP BY Bucket;
        SELECT COUNT_BIG(*) AS Total, COALESCE(@watermark, 0) AS SnapshotRawId FROM #HistoryTimes;

        WITH ParameterIds AS (
            SELECT D.SensorParameterID
            FROM dbo.TrnLoggerRawHdr H
            INNER JOIN dbo.TrnLoggerRawDtl D ON D.RawID = H.RawID
            INNER JOIN dbo.TrnLoggerHasil R ON R.RawDtlID = D.RawDtlID
            WHERE H.LoggerID = @loggerId AND H.RawID <= @watermark
              AND H.TanggalSensor >= @activeSince AND H.TanggalSensor <= @now
              AND R.IsCurrent = 1
              AND R.StatusHasil = 'OK' AND R.NilaiHasil IS NOT NULL
            GROUP BY D.SensorParameterID
        )
        SELECT P.SensorParameterID, P.ParameterCode, P.NamaParameter, V.Satuan
        FROM ParameterIds I
        INNER JOIN dbo.MstSensorParameter P ON P.SensorParameterID = I.SensorParameterID
        OUTER APPLY (
            SELECT TOP (1) Satuan FROM dbo.vwLoggerDataDetail
            WHERE LoggerID = @loggerId AND SensorParameterID = I.SensorParameterID
              AND NULLIF(LTRIM(RTRIM(Satuan)), '') IS NOT NULL
            ORDER BY SensorCode, ParameterCode, Satuan
        ) V
        ORDER BY P.SensorParameterID;

        SELECT Bucket INTO #HistoryPage FROM #HistoryTimes
        ORDER BY Bucket {{(ascending ? "ASC" : "DESC")}}
        OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;

        WITH Ranked AS (
            SELECT H.Bucket, H.RawID, H.ReceivedAt, D.SensorParameterID, R.NilaiHasil,
                ROW_NUMBER() OVER (
                    PARTITION BY H.LoggerID, H.TanggalSensor, D.SensorParameterID
                    ORDER BY R.ProcessedAt DESC, R.HasilID DESC, H.ReceivedAt DESC,
                        H.RawID DESC, D.RawDtlID DESC
                ) AS Revision
            FROM #HistoryHeaders H
            INNER JOIN #HistoryPage T ON T.Bucket = H.Bucket
            LEFT JOIN dbo.TrnLoggerRawDtl D ON D.RawID = H.RawID
            OUTER APPLY (
                SELECT TOP (1) X.NilaiHasil, X.ProcessedAt, X.HasilID
                FROM dbo.TrnLoggerHasil X
                WHERE X.RawDtlID = D.RawDtlID AND X.IsCurrent = 1
                ORDER BY X.ProcessedAt DESC, X.HasilID DESC
            ) R
        )
        SELECT Bucket, SensorParameterID, MAX(RawID), MAX(ReceivedAt),
            CASE WHEN @aggregation = 'sample' THEN MAX(NilaiHasil)
                 WHEN @aggregationMode = 'sum' THEN SUM(NilaiHasil)
                 ELSE AVG(NilaiHasil) END AS Value
        FROM Ranked WHERE Revision = 1
        GROUP BY Bucket, SensorParameterID
        ORDER BY Bucket {{(ascending ? "ASC" : "DESC")}}, SensorParameterID;
        DROP TABLE #HistoryPage, #HistoryTimes, #HistoryHeaders;
        """;

    public static async Task<Result> Read(DbDataReader reader, int loggerId, int page, int pageSize, string aggregation, CancellationToken ct)
    {
        await reader.ReadAsync(ct);
        var total = reader.GetInt64(0);
        var snapshot = reader.GetInt64(1);
        await reader.NextResultAsync(ct);
        var parameters = new List<Parameter>();
        while (await reader.ReadAsync(ct))
            parameters.Add(new Parameter(reader.GetInt32(0), Text(reader, 1), Text(reader, 2), Text(reader, 3), true));
        await reader.NextResultAsync(ct);
        var rows = new List<Row>();
        Row? current = null;
        while (await reader.ReadAsync(ct))
        {
            var ts = ToUtc(reader.GetDateTime(0));
            if (current?.Ts != ts)
            {
                current = new Row(loggerId, ts, new Dictionary<string, string?>());
                rows.Add(current);
            }
            if (!reader.IsDBNull(1))
                current.Data[reader.GetInt32(1).ToString(CultureInfo.InvariantCulture)] = reader.IsDBNull(4)
                    ? null : reader.GetDecimal(4).ToString(CultureInfo.InvariantCulture);
        }
        return new Result(loggerId, total, page, pageSize, snapshot, aggregation, "Asia/Jakarta", parameters, rows);
    }

    private static string? Text(DbDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetString(index).Trim();
    public sealed record Parameter(int SensorParameterId, string? ParameterCode, string? NamaParameter, string? Satuan, bool Active);
    public sealed record Row(int LoggerId, DateTime Ts, Dictionary<string, string?> Data);
    public sealed record Result(int LoggerId, long Total, int Page, int PageSize, long SnapshotRawId, string Aggregation, string TimeZone, IReadOnlyList<Parameter> Parameters, IReadOnlyList<Row> Data);
}
