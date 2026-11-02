
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json.Serialization;
using AuthUtility.Application.Interfaces;
using AuthUtility.Api.IoT;
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
    // IoT Management adalah area admin read-only, sedangkan menu WAD juga
    // membutuhkan snapshot monitoring yang sama untuk halaman operasionalnya.
    private static readonly string[] ReadMenuCodes =
    {
        "DASHBOARD_IOT",
        "WAD_ECS_DASHBOARD",
        "WAD_ECS_STASIUN",
        "WAD_ECS_MONITOR_CH",
        "WAD_ECS_DATA_HISTORIS",
        "WAD_ECS_KALENDER_CH",
        "WAD_ECS_PETA",
        "WAD_ECS_CURAH_HUJAN",
        "WAD_ECS_TMAK",
        "WAD_ECS_KELEMBAPAN",
    };

    private async Task<bool> HasAccess()
    {
        foreach (var code in ReadMenuCodes)
        {
            if (await permissions.HasPermissionAsync(User, code, MenuPermission.Read)) return true;
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
                L.Latitude,
                L.Longitude,
                L.IntervalMenit,
                L.Aktif,

                H.RawID,
                H.TanggalSensor,
                H.ReceivedAt,

                DATEDIFF(
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
                    double? latitude = Number(reader, 5);
                    double? longitude = Number(reader, 6);

                    int interval = reader.IsDBNull(7)
                        ? 0
                        : reader.GetInt32(7);

                    bool aktif = !reader.IsDBNull(8)
                        && reader.GetBoolean(8);

                    long? ageSeconds = reader.IsDBNull(12)
                        ? null
                        : reader.GetInt32(12);

                    device = new MonitoringDevice
                    {
                        LoggerId = loggerId,
                        LoggerCode = GetString(reader, 1),
                        NamaLogger = GetString(reader, 2),
                        SourceType = GetString(reader, 3),
                        Lokasi = GetString(reader, 4),
                        Latitude = latitude,
                        Longitude = longitude,
                        IntervalMenit = interval,
                        Aktif = aktif,
                        RawId = reader.IsDBNull(9)
                            ? null
                            : reader.GetInt64(9),
                        LastSensorAt = GetDate(reader, 10),
                        LastReceivedAt = GetDate(reader, 11),
                        AgeMinutes = ageSeconds.HasValue
                            ? Math.Round(
                                Math.Max(0L, ageSeconds.Value) / 60.0,
                                1)
                            : null
                    };

                    device.Status = IotMonitoringRules.DetermineStatus(
                        aktif,
                        interval,
                        device.AgeMinutes);

                    lookup.Add(loggerId, device);
                    devices.Add(device);
                }

                if (reader.IsDBNull(13))
                    continue;

                long rawDtlId = reader.GetInt64(13);

                int parameterId = reader.IsDBNull(14)
                    ? 0
                    : reader.GetInt32(14);

                string code =
                    GetString(reader, 15) ?? "PARAMETER";

                string key = code;

                // Hindari kehilangan parameter jika kode
                // yang sama muncul lebih dari sekali.
                if (device.Data.ContainsKey(key))
                    key = $"{code}#{parameterId}";

                if (device.Data.ContainsKey(key))
                    key = $"{code}#{parameterId}#{rawDtlId}";

                decimal? value = GetDecimal(reader, 16);
                string? statusData = GetString(reader, 17);
                string? statusHasil = GetString(reader, 18);

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

    private static double? Number(
        DbDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
            return null;

        var value = reader.GetValue(index);
        if (value is double number)
            return number;
        if (value is float single)
            return single;
        if (value is decimal decimalNumber)
            return (double)decimalNumber;

        return double.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    public sealed class MonitoringDevice
    {
        public int LoggerId { get; set; }
        public string? LoggerCode { get; set; }
        public string? NamaLogger { get; set; }
        public string? SourceType { get; set; }
        public string? Lokasi { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
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
