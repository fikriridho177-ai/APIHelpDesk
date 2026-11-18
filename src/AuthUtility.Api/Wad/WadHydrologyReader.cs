using System.Data;
using System.Data.Common;
using System.Globalization;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Wad;

// Membaca nilai terbaru dari view eksisting MyKTTATest. Tidak ada tabel salinan
// dan tidak ada nilai nol pengganti ketika view tidak memiliki pos terkait.
public static class WadHydrologyReader
{
    public sealed record TmakPoint(string Pos, decimal? Nilai, DateTime? Tanggal, string? Keterangan);
    public sealed record TmatPoint(string Pos, string Wilayah, decimal? Nilai, DateTime? Tanggal);
    public sealed record RainPoint(string Pos, DateTime Tanggal, decimal? Pagi, decimal? Sore);

    public static async Task<Dictionary<string, TmakPoint>> ReadLatestTmak(
        MyKTTATestDbContext db,
        CancellationToken ct)
    {
        const string sql = """
            SELECT Pos, Nilai, Tanggal, Keterangan
            FROM (
                SELECT
                    RTRIM(KodeBlok) AS Pos,
                    Nilai,
                    Tanggal,
                    ket AS Keterangan,
                    ROW_NUMBER() OVER (PARTITION BY RTRIM(KodeBlok) ORDER BY Tanggal DESC, Nilai DESC) AS RowNumber
                FROM vw_ECSMonTmak
            ) AS Latest
            WHERE RowNumber = 1
            """;

        var result = new Dictionary<string, TmakPoint>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in await ReadRowsAsync(db, sql, ct: ct))
        {
            var pos = Text(row, 0);
            if (pos == null) continue;
            result[pos] = new TmakPoint(
                pos,
                Decimal(row, 1),
                Date(row, 2),
                Text(row, 3));
        }
        return result;
    }

    public static async Task<Dictionary<string, TmatPoint>> ReadLatestTmat(
        MyKTTATestDbContext db,
        CancellationToken ct)
    {
        const string sql = """
            SELECT Pos, Wilayah, Nilai, Tanggal
            FROM (
                SELECT
                    RTRIM(KodePersil) AS Pos,
                    RTRIM(KodeWilayah) AS Wilayah,
                    Nilai,
                    Tanggal,
                    ROW_NUMBER() OVER (PARTITION BY RTRIM(KodePersil) ORDER BY Tanggal DESC, Nilai DESC) AS RowNumber
                FROM vw_ECSMonTmat
            ) AS Latest
            WHERE RowNumber = 1
            """;

        var result = new Dictionary<string, TmatPoint>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in await ReadRowsAsync(db, sql, ct: ct))
        {
            var pos = Text(row, 0);
            if (pos == null) continue;
            result[pos] = new TmatPoint(pos, Text(row, 1) ?? "", Decimal(row, 2), Date(row, 3));
        }
        return result;
    }

    public static async Task<List<RainPoint>> ReadLatestRain(
        MyKTTATestDbContext db,
        DateTime? startDate,
        CancellationToken ct)
    {
        const string sql = """
            SELECT RTRIM(KodePersil) AS Pos, Tanggal, Pagi, Sore
            FROM vw_ECSMonCurahHujan
            WHERE (@startDate IS NULL OR Tanggal >= @startDate)
            ORDER BY Tanggal
            """;

        var result = new List<RainPoint>();
        foreach (var row in await ReadRowsAsync(db, sql, ct, ("@startDate", (object?)startDate ?? DBNull.Value)))
        {
            var pos = Text(row, 0);
            var date = Date(row, 1);
            if (pos == null || date == null) continue;
            if (startDate != null && date.Value.Date < startDate.Value.Date) continue;
            result.Add(new RainPoint(pos, date.Value, Decimal(row, 2), Decimal(row, 3)));
        }
        return result;
    }

    public static WadAirSensorValue ToValue(
        string? reference,
        Dictionary<string, TmakPoint> values,
        string unit) =>
        ToSensorValue(reference, match => values.TryGetValue(match, out var point)
            ? new SensorRead(point.Nilai, point.Tanggal, point.Pos)
            : null, unit);

    public static WadAirSensorValue ToValue(
        string? reference,
        Dictionary<string, TmatPoint> values,
        string unit) =>
        ToSensorValue(reference, match => values.TryGetValue(match, out var point)
            ? new SensorRead(point.Nilai, point.Tanggal, point.Pos)
            : null, unit);

    private sealed record SensorRead(decimal? Nilai, DateTime? Tanggal, string Lokasi);

    private static WadAirSensorValue ToSensorValue(
        string? reference,
        Func<string, SensorRead?> resolve,
        string unit)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return new WadAirSensorValue(null, unit, null, null);
        var value = resolve(reference.Trim());
        return new WadAirSensorValue(
            value?.Nilai,
            unit,
            value?.Lokasi ?? reference.Trim(),
            value?.Tanggal?.ToString("yyyy-MM-dd"));
    }

    public static WadAirRainValue BuildRegionRain(
        string wilayah,
        List<RainPoint> points,
        DateTime? asOfDate)
    {
        var prefix = wilayah switch
        {
            "Wilayah 1" => "W1",
            "Wilayah 2" => "W2",
            "Wilayah 3" => "W3",
            "Wilayah 4" => "W4",
            _ => null,
        };
        if (prefix == null) return new WadAirRainValue(null, null, null, 0);

        var regionPoints = points
            .Where(point => point.Pos.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (regionPoints.Count == 0) return new WadAirRainValue(null, null, null, 0);

        var latestDate = regionPoints.Max(point => point.Tanggal.Date);
        var latestTotals = regionPoints
            .Where(point => point.Tanggal.Date == latestDate)
            .Select(point => Total(point.Pagi, point.Sore))
            .Where(value => value != null)
            .Select(value => value!.Value)
            .ToList();
        decimal? rainfall = latestTotals.Count == 0
            ? null
            : Math.Round(latestTotals.Average(), 1, MidpointRounding.AwayFromZero);

        var windowEnd = asOfDate?.Date ?? latestDate;
        var windowStart = windowEnd.AddDays(-29);
        var rainyDays = regionPoints
            .Where(point => point.Tanggal.Date >= windowStart && point.Tanggal.Date <= windowEnd)
            .GroupBy(point => point.Tanggal.Date)
            .Count(group => group.Any(point => Total(point.Pagi, point.Sore) > 0));

        return new WadAirRainValue(
            rainfall,
            rainyDays,
            latestDate.ToString("yyyy-MM-dd"),
            regionPoints.Count);
    }

    private static decimal? Total(decimal? pagi, decimal? sore)
    {
        var values = new[] { pagi, sore }.Where(value => value != null).Select(value => value!.Value).ToList();
        return values.Count == 0 ? null : values.Sum();
    }

    private static async Task<List<object?[]>> ReadRowsAsync(
        MyKTTATestDbContext db,
        string sql,
        CancellationToken ct,
        params (string Name, object Value)[] parameters)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 120;
        foreach (var parameter in parameters)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = parameter.Name;
            dbParameter.Value = parameter.Value;
            command.Parameters.Add(dbParameter);
        }

        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<object?[]>();
        while (await reader.ReadAsync(ct))
        {
            var values = new object?[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index += 1)
                values[index] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            rows.Add(values);
        }
        return rows;
    }

    private static string? Text(object?[] row, int index) =>
        row[index] == null ? null : Convert.ToString(row[index], CultureInfo.InvariantCulture)?.Trim();

    private static decimal? Decimal(object?[] row, int index)
    {
        if (row[index] == null) return null;
        try { return Convert.ToDecimal(row[index], CultureInfo.InvariantCulture); }
        catch (FormatException) { return null; }
    }

    private static DateTime? Date(object?[] row, int index)
    {
        if (row[index] == null) return null;
        return row[index] switch
        {
            DateTime value => value,
            DateOnly value => value.ToDateTime(TimeOnly.MinValue),
            _ => DateTime.TryParse(Convert.ToString(row[index], CultureInfo.InvariantCulture), out var parsed)
                ? parsed
                : null,
        };
    }
}
