using System.Data;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Wad;

public static class WadDashboardService
{
    public static readonly string[] AttentionConditions =
    {
        "Rusak Parah",
        "Kurang baik",
        "Dalam tahap perbaikan",
        "Kebocoran semakin besar",
        "Longsor tanah samping dan jebol dinding overflow",
    };

    public static bool NeedsAttention(string? condition) =>
        !string.IsNullOrWhiteSpace(condition)
        && AttentionConditions.Contains(condition.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string? AlertLevel(string? condition)
    {
        if (!NeedsAttention(condition)) return null;
        var normalized = condition!.Trim().ToLowerInvariant();
        return normalized.Contains("kurang baik") || normalized.Contains("perbaikan")
            ? "warning"
            : "critical";
    }

    public static async Task<WadHubSummaryDto> BuildAsync(
        AppDbContext db,
        MyKTTATestDbContext iotDb,
        CancellationToken ct)
    {
        var logger = await ReadLoggerSummaryAsync(iotDb, ct);

        var latestRows = await db.WadOverflowLogs
            .FromSqlRaw("""
                SELECT DISTINCT ON (kode_aset) *
                FROM trx_wad_log_overflow
                ORDER BY kode_aset, tanggal_log DESC, id DESC
                """)
            .AsNoTracking()
            .ToListAsync(ct);

        var kodeAset = latestRows.Select(x => x.KodeAset).Distinct().ToList();
        var buildings = await db.WadBuildings.AsNoTracking()
            .Where(x => kodeAset.Contains(x.KodeAset))
            .ToDictionaryAsync(x => x.KodeAset, ct);

        var attentionItems = latestRows
            .Where(x => NeedsAttention(x.KondisiFisik) && buildings.ContainsKey(x.KodeAset))
            .OrderBy(x => buildings[x.KodeAset].SortOrder)
            .Select(x =>
            {
                var building = buildings[x.KodeAset];
                return new WadHubAttentionItem(
                    x.KodeAset,
                    building.Wilayah,
                    building.Kanal,
                    building.NamaBangunan,
                    x.KondisiFisik,
                    x.TanggalLog.ToString("yyyy-MM-dd"),
                    AlertLevel(x.KondisiFisik) ?? "warning");
            })
            .ToList();

        // Menu WAD dapat berada di bawah parent lama sebelum seed grup WAD dijalankan.
        var wadMenus = db.Menus.AsNoTracking()
            .Where(x => x.Active && (x.Code == "WAD" || x.Code.StartsWith("WAD_")));
        var totalServices = await wadMenus.CountAsync(x => x.MenuType == "MENU", ct);
        var activeServices = await wadMenus.CountAsync(x => x.MenuType == "MENU" && x.Route != null, ct);

        var wilayah = await db.WadBuildings.AsNoTracking()
            .Where(x => x.Aktif)
            .Select(x => x.Wilayah)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);
        var latestWeek = await db.WadOverflowLogs.AsNoTracking()
            .OrderByDescending(x => x.TanggalLog)
            .ThenByDescending(x => x.Id)
            .Select(x => x.Minggu)
            .FirstOrDefaultAsync(ct);
        var pengamatanUnits = await db.WadBuildings.AsNoTracking().CountAsync(x => x.Aktif, ct);
        var subsidenStakes = await db.WadSubsidenObservations.AsNoTracking()
            .Select(x => x.KodePatok)
            .Distinct()
            .CountAsync(ct);

        var deviceTotal = await db.IotDeviceAgents.AsNoTracking().CountAsync(ct);
        var firmwareVersion = await db.IotDeviceAgents.AsNoTracking()
            .Where(x => x.LastFirmwareVersion != null)
            .GroupBy(x => x.LastFirmwareVersion!)
            .OrderByDescending(x => x.Count())
            .Select(x => x.Key)
            .FirstOrDefaultAsync(ct);

        var todayWib = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var latestManualDate = await db.WadOverflowLogs.AsNoTracking()
            .OrderByDescending(x => x.TanggalLog)
            .Select(x => (DateOnly?)x.TanggalLog)
            .FirstOrDefaultAsync(ct);
        var officerWindow = (latestManualDate ?? todayWib).AddDays(-29);
        var recentManualRows = await db.WadOverflowLogs.AsNoTracking()
            .Where(x => x.TanggalLog >= officerWindow)
            .Select(x => new { x.TanggalLog, x.PetugasNama })
            .ToListAsync(ct);
        var activeOfficers = recentManualRows
            .Select(x => x.PetugasNama?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var reportsToday = recentManualRows.Count(x => x.TanggalLog == todayWib);

        return new WadHubSummaryDto(
            logger,
            new WadHubAttention(
                logger.Offline + attentionItems.Count,
                logger.Offline,
                attentionItems.Count,
                attentionItems),
            new WadHubServices(activeServices, totalServices),
            new WadHubCoverage(wilayah, latestWeek),
            new WadHubFieldSummary(pengamatanUnits, subsidenStakes, activeOfficers, reportsToday),
            new WadHubDevices(deviceTotal, firmwareVersion),
            DateTime.UtcNow);
    }

    private static async Task<WadHubLoggerSummary> ReadLoggerSummaryAsync(
        MyKTTATestDbContext db,
        CancellationToken ct)
    {
        const string sql = """
            SELECT
                L.Aktif,
                L.IntervalMenit,
                L.SourceType,
                X.AgeSeconds,
                X.ReceivedAt
            FROM dbo.MstLogger AS L
            OUTER APPLY
            (
                SELECT TOP (1)
                    DATEDIFF(SECOND, H.ReceivedAt, SYSUTCDATETIME()) AS AgeSeconds,
                    H.ReceivedAt
                FROM dbo.TrnLoggerRawHdr AS H
                WHERE H.LoggerID = L.LoggerID
                ORDER BY H.ReceivedAt DESC, H.RawID DESC
            ) AS X
            """;

        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);

        var total = 0;
        var activeCount = 0;
        var online = 0;
        var vendor = 0;
        DateTime? lastReadingAt = null;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = sql;
            command.CommandTimeout = 60;
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                total += 1;
                var active = reader.GetBoolean(0);
                var interval = reader.GetInt32(1);
                var sourceType = reader.IsDBNull(2) ? null : reader.GetString(2).Trim().ToUpperInvariant();
                if (active) activeCount += 1;
                if (sourceType == "VENDOR") vendor += 1;
                var age = reader.IsDBNull(3) ? (double?)null : reader.GetInt32(3);
                if (active && age.HasValue && interval > 0 && age.Value <= interval * 2.0) online += 1;
                if (!reader.IsDBNull(4))
                {
                    var receivedAt = reader.GetDateTime(4);
                    if (lastReadingAt is null || receivedAt > lastReadingAt) lastReadingAt = receivedAt;
                }
            }
        }

        var parameters = 0;
        try
        {
            await using var paramCommand = connection.CreateCommand();
            paramCommand.CommandText = """
                SELECT COUNT(DISTINCT V.SensorParameterID)
                FROM dbo.vwLoggerDataDetail AS V
                INNER JOIN dbo.MstLogger AS L ON L.LoggerID = V.LoggerID
                WHERE L.Aktif = 1
                """;
            paramCommand.CommandTimeout = 60;
            parameters = Convert.ToInt32(await paramCommand.ExecuteScalarAsync(ct) ?? 0);
        }
        catch (Exception)
        {
            parameters = 0;
        }

        var uptime = activeCount > 0 ? Math.Round(online * 100.0 / activeCount, 1) : 0;

        return new WadHubLoggerSummary(
            total,
            activeCount,
            online,
            Math.Max(0, total - online),
            Math.Max(0, total - vendor),
            vendor,
            parameters,
            uptime,
            lastReadingAt);
    }
}
