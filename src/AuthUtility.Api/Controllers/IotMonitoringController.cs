
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json.Serialization;
using AuthUtility.Application.Interfaces;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/iot/monitoring")]
public class IotMonitoringController(
    MyKTTATestDbContext db,
    IPermissionService permissions,
    ILogger<IotMonitoringController> logger
) : ControllerBase
{
    // Sementara mengikuti permission baca ECS.
    // Ganti dengan permission khusus IoT sebelum produksi.
    private async Task<bool> HasAccess()
    {
        foreach (var code in EcsController.MenuCodes)
        {
            if (await permissions.HasPermissionAsync(
                User, code, MenuPermission.Read))
                return true;
        }

        return false;
    }

    // GET /api/iot/monitoring
    // GET /api/iot/monitoring?sourceType=VENDOR
    // GET /api/iot/monitoring?sourceType=INTERNAL
    [HttpGet]
    public async Task<IActionResult> GetMonitoring(
        [FromQuery] string? sourceType = null)
    {
        if (!await HasAccess())
            return Forbid();

        sourceType = sourceType?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(sourceType))
            sourceType = null;

        if (sourceType != null &&
            sourceType != "VENDOR" &&
            sourceType != "INTERNAL")
        {
            return BadRequest(new
            {
                message = "sourceType harus VENDOR atau INTERNAL."
            });
        }

        const string sql = """
            SELECT
                L.LoggerID,
                L.LoggerCode,
                L.NamaLogger,
                L.SourceType,
                L.Lokasi,
                L.IntervalMenit,
                L.Aktif,

                H.RawID,
                H.TanggalSensor,
                H.ReceivedAt,

                DATEDIFF_BIG(
                    SECOND, H.ReceivedAt, GETDATE()
                ) AS AgeSeconds,

                D.RawDtlID,
                D.SensorParameterID,
                P.ParameterCode,

                R.NilaiHasil,
                D.StatusData,
                R.StatusHasil

            FROM dbo.MstLogger AS L

            OUTER APPLY
            (
                SELECT TOP (1)
                    RH.RawID,
                    RH.TanggalSensor,
                    RH.ReceivedAt
                FROM dbo.TrnLoggerRawHdr AS RH
                WHERE RH.LoggerID = L.LoggerID
                ORDER BY
                    RH.TanggalSensor DESC,
                    RH.RawID DESC
            ) AS H

            LEFT JOIN dbo.TrnLoggerRawDtl AS D
                ON D.RawID = H.RawID

            LEFT JOIN dbo.MstSensorParameter AS P
                ON P.SensorParameterID = D.SensorParameterID

            OUTER APPLY
            (
                SELECT TOP (1)
                    X.NilaiHasil,
                    X.StatusHasil
                FROM dbo.TrnLoggerHasil AS X
                WHERE X.RawDtlID = D.RawDtlID
                  AND X.IsCurrent = 1
                ORDER BY
                    X.ProcessedAt DESC,
                    X.HasilID DESC
            ) AS R

            WHERE
                (@sourceType IS NULL
                    OR L.SourceType = @sourceType)

            ORDER BY
                L.SourceType,
                L.LoggerCode,
                D.RawDtlID;
            """;

        try
        {
            var connection = db.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(
                    HttpContext.RequestAborted);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 60;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@sourceType";
            parameter.DbType = DbType.String;
            parameter.Value = (object?)sourceType ?? DBNull.Value;
            command.Parameters.Add(parameter);

            await using var reader =
                await command.ExecuteReaderAsync(
                    HttpContext.RequestAborted);

            var devices = new List<MonitoringDevice>();
            var lookup = new Dictionary<int, MonitoringDevice>();

            while (await reader.ReadAsync(
                HttpContext.RequestAborted))
            {
                int loggerId = reader.GetInt32(0);

                if (!lookup.TryGetValue(loggerId, out var device))
                {
                    int interval = reader.IsDBNull(5)
                        ? 0
                        : reader.GetInt32(5);

                    bool aktif = !reader.IsDBNull(6)
                        && reader.GetBoolean(6);

                    long? ageSeconds = reader.IsDBNull(10)
                        ? null
                        : reader.GetInt64(10);

                    device = new MonitoringDevice
                    {
                        LoggerId = loggerId,
                        LoggerCode = GetString(reader, 1),
                        NamaLogger = GetString(reader, 2),
                        SourceType = GetString(reader, 3),
                        Lokasi = GetString(reader, 4),
                        IntervalMenit = interval,
                        Aktif = aktif,
                        RawId = reader.IsDBNull(7)
                            ? null
                            : reader.GetInt64(7),
                        LastSensorAt = GetDate(reader, 8),
                        LastReceivedAt = GetDate(reader, 9),
                        AgeMinutes = ageSeconds.HasValue
                            ? Math.Round(
                                Math.Max(0L, ageSeconds.Value) / 60.0,
                                1)
                            : null
                    };

                    device.Status = DetermineStatus(
                        aktif,
                        interval,
                        device.AgeMinutes);

                    lookup.Add(loggerId, device);
                    devices.Add(device);
                }

                if (reader.IsDBNull(11))
                    continue;

                long rawDtlId = reader.GetInt64(11);

                int parameterId = reader.IsDBNull(12)
                    ? 0
                    : reader.GetInt32(12);

                string code =
                    GetString(reader, 13) ?? "PARAMETER";

                string key = code;

                // Hindari kehilangan parameter jika kode
                // yang sama muncul lebih dari sekali.
                if (device.Data.ContainsKey(key))
                    key = $"{code}#{parameterId}";

                if (device.Data.ContainsKey(key))
                    key = $"{code}#{parameterId}#{rawDtlId}";

                decimal? value = GetDecimal(reader, 14);
                string? statusData = GetString(reader, 15);
                string? statusHasil = GetString(reader, 16);

                device.Data[key] = value;

                string? error = null;

                if (statusData != "OK")
                    error = statusData ?? "UNKNOWN_DATA_STATUS";
                else if (value is null)
                    error = "NO_RESULT";
                else if (statusHasil != "OK")
                    error = statusHasil ?? "UNKNOWN_RESULT_STATUS";

                if (error != null)
                {
                    device.Errors ??= new();
                    device.Errors[key] = error;
                }
            }

            return Ok(new
            {
                total = devices.Count,
                online = devices.Count(
                    d => d.Status == "ONLINE"),
                stale = devices.Count(
                    d => d.Status == "STALE"),
                noRecentData = devices.Count(
                    d => d.Status == "NO_RECENT_DATA"),
                inactive = devices.Count(
                    d => d.Status == "INACTIVE"),
                devices
            });
        }
        catch (DbException ex)
        {
            logger.LogError(
                ex,
                "Gagal membaca monitoring IoT terpadu");

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message = "Gagal membaca monitoring IoT."
                });
        }
    }

    private static string DetermineStatus(
        bool aktif,
        int interval,
        double? ageMinutes)
    {
        if (!aktif)
            return "INACTIVE";

        if (ageMinutes is null)
            return "NO_RECENT_DATA";

        // Jika interval tidak valid, jangan
        // menganggap perangkat ONLINE.
        if (interval <= 0)
            return "UNKNOWN";

        if (ageMinutes <= interval * 2.0)
            return "ONLINE";

        if (ageMinutes <= interval * 6.0)
            return "STALE";

        return "NO_RECENT_DATA";
    }

    private static string? GetString(
        DbDataReader reader, int index)
    {
        return reader.IsDBNull(index)
            ? null
            : reader.GetString(index).Trim();
    }

    private static DateTime? GetDate(
        DbDataReader reader, int index)
    {
        return reader.IsDBNull(index)
            ? null
            : reader.GetDateTime(index);
    }

    private static decimal? GetDecimal(
        DbDataReader reader, int index)
    {
        return reader.IsDBNull(index)
            ? null
            : reader.GetDecimal(index);
    }

    public sealed class MonitoringDevice
    {
        public int LoggerId { get; set; }
        public string? LoggerCode { get; set; }
        public string? NamaLogger { get; set; }
        public string? SourceType { get; set; }
        public string? Lokasi { get; set; }
        public int IntervalMenit { get; set; }
        public bool Aktif { get; set; }

        public string Status { get; set; } = "UNKNOWN";

        public long? RawId { get; set; }
        public DateTime? LastSensorAt { get; set; }
        public DateTime? LastReceivedAt { get; set; }
        public double? AgeMinutes { get; set; }

        public Dictionary<string, decimal?> Data
            { get; set; } = new();

        [JsonIgnore(
            Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string>? Errors { get; set; }
    }
}
