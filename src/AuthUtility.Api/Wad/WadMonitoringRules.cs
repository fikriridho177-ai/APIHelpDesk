namespace AuthUtility.Api.Wad;

public sealed record WadMonitoringSource(
    string Parameter,
    string ViewName,
    string PositionColumn,
    string? RegionColumn,
    string ValueColumn,
    string? SecondaryValueColumn,
    string? CoordinateXColumn,
    string? CoordinateYColumn,
    string? KeteranganColumn,
    string? UserIdColumn
);

public static class WadMonitoringRules
{
    private static readonly IReadOnlyDictionary<string, WadMonitoringSource> SourceMap =
        new Dictionary<string, WadMonitoringSource>(StringComparer.OrdinalIgnoreCase)
        {
            ["curah-hujan"] = new(
                "curah-hujan",
                "vw_ECSMonCurahHujan",
                "KodePersil",
                null,
                "Pagi",
                "Sore",
                "KoordinatX",
                "KoordinatY",
                null,
                "UserID"),
            ["tmak"] = new(
                "tmak",
                "vw_ECSMonTmak",
                "KodeBlok",
                null,
                "Nilai",
                null,
                "KoordinatX",
                "KoordinatY",
                "Ket",
                null),
            ["tmat"] = new(
                "tmat",
                "vw_ECSMonTmat",
                "KodePersil",
                "KodeWilayah",
                "Nilai",
                null,
                "KoordinatX",
                "KoordinatY",
                "ket",
                null),
        };

    public static bool TryGetSource(string? parameter, out WadMonitoringSource source)
    {
        var normalized = string.IsNullOrWhiteSpace(parameter)
            ? "curah-hujan"
            : parameter.Trim();

        return SourceMap.TryGetValue(normalized, out source!);
    }

    public static string BuildQuery(
        WadMonitoringSource source,
        bool hasStartDate,
        bool hasEndDate,
        bool hasWilayah,
        bool hasPos,
        bool isLatestOnly = false)
    {
        if (!SourceMap.Values.Contains(source))
            throw new ArgumentException("Sumber monitoring WAD tidak dikenal.", nameof(source));

        if (hasWilayah && source.RegionColumn is null)
            throw new ArgumentException(
                $"Filter wilayah belum tersedia pada view {source.ViewName}.",
                nameof(hasWilayah));

        var where = new List<string> { "1 = 1" };
        if (hasStartDate) where.Add("v.Tanggal >= @startDate");
        if (hasEndDate) where.Add("v.Tanggal < DATEADD(DAY, 1, @endDate)");
        if (hasWilayah) where.Add($"v.{source.RegionColumn} = @wilayah");
        if (hasPos) where.Add($"v.{source.PositionColumn} = @pos");

        var secondaryValue = source.SecondaryValueColumn is null
            ? "CAST(NULL AS decimal(18, 6))"
            : $"v.{source.SecondaryValueColumn}";
        var keterangan = source.KeteranganColumn is null
            ? "CAST(NULL AS nvarchar(500))"
            : $"v.{source.KeteranganColumn}";
        var userId = source.UserIdColumn is null
            ? "CAST(NULL AS nvarchar(255))"
            : $"v.{source.UserIdColumn}";
        var coordinateX = source.CoordinateXColumn is null
            ? "CAST(NULL AS decimal(18, 6))"
            : $"v.{source.CoordinateXColumn}";
        var coordinateY = source.CoordinateYColumn is null
            ? "CAST(NULL AS decimal(18, 6))"
            : $"v.{source.CoordinateYColumn}";

        var projection = $"""
                '{source.Parameter}' AS parameter,
                v.Tanggal AS tanggal,
                v.{source.PositionColumn} AS pos,
                {(source.RegionColumn is null ? "CAST(NULL AS nvarchar(255))" : $"v.{source.RegionColumn}")} AS wilayah,
                v.{source.ValueColumn} AS nilai,
                {secondaryValue} AS nilaiSekunder,
                {coordinateX} AS koordinatX,
                {coordinateY} AS koordinatY,
                {keterangan} AS keterangan,
                {userId} AS userId,
                CASE
                    WHEN v.Tanggal >= DATEADD(MINUTE, -@onlineWindowMinutes, SYSDATETIME())
                    THEN CAST(1 AS bit)
                    ELSE CAST(0 AS bit)
                END AS isOnline
            """;

        var partitionBy = source.RegionColumn is null
            ? $"v.{source.PositionColumn}"
            : $"v.{source.RegionColumn}, v.{source.PositionColumn}";

        var tieBreakers = new List<string>
        {
            $"v.{source.ValueColumn} DESC",
        };

        if (source.SecondaryValueColumn is not null)
            tieBreakers.Add($"v.{source.SecondaryValueColumn} DESC");
        if (source.UserIdColumn is not null)
            tieBreakers.Add($"v.{source.UserIdColumn} DESC");
        if (source.CoordinateXColumn is not null)
            tieBreakers.Add($"v.{source.CoordinateXColumn} DESC");
        if (source.CoordinateYColumn is not null)
            tieBreakers.Add($"v.{source.CoordinateYColumn} DESC");

        if (isLatestOnly)
        {
            return $"""
                WITH RankedRows AS
                (
                    SELECT
                        {projection},
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY {partitionBy}
                            ORDER BY
                                v.Tanggal DESC,
                                {string.Join(",\n                                ", tieBreakers)}
                        ) AS RowNumber
                    FROM dbo.{source.ViewName} AS v
                    WHERE {string.Join(" AND ", where)}
                )
                SELECT
                    parameter,
                    tanggal,
                    pos,
                    wilayah,
                    nilai,
                    nilaiSekunder,
                    koordinatX,
                    koordinatY,
                    keterangan,
                    userId,
                    isOnline
                FROM RankedRows
                WHERE RowNumber = 1
                ORDER BY tanggal DESC, pos;
                """;
        }

        return $"""
            SELECT
                {projection}
            FROM dbo.{source.ViewName} AS v
            WHERE {string.Join(" AND ", where)}
            ORDER BY v.Tanggal, v.{source.PositionColumn};
            """;
    }
}
