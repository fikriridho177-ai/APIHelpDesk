
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
[Route("api/iot/devices")]
[Authorize]
public class IotDeviceController(
    MyKTTATestDbContext db,
    IPermissionService permissions,
    ILogger<IotDeviceController> logger
) : ControllerBase
{
    // IoT Management adalah area admin read-only.
    // User biasa tetap mengakses monitoring melalui menu-menu WAD.
    private const string SuperAdminGateMenuCode = "DASHBOARD_IOT";

    private async Task<bool> HasAccess()
    {
        var permission = await permissions.GetPermissionsAsync(
            User,
            SuperAdminGateMenuCode);

        return permission?.IsSuperAdmin == true;
    }

    // =====================================================
    // GET /api/iot/devices?sourceType=INTERNAL
    // =====================================================
    [HttpGet]
    public async Task<IActionResult> GetDevices(
        [FromQuery] string? sourceType = null)
    {
        if (!await HasAccess()) return Forbid();

        sourceType = sourceType?.Trim().ToUpperInvariant();
        if (sourceType == "") sourceType = null;

        if (sourceType is not null
            and not "VENDOR"
            and not "INTERNAL")
        {
            return BadRequest(new
            {
                message = "sourceType harus VENDOR atau INTERNAL."
            });
        }

        const string sql = """
            SELECT
                LoggerID, LoggerCode, NamaLogger,
                SourceType, Lokasi, Latitude, Longitude,
                Controller, Communication, IntervalMenit, Aktif
            FROM dbo.MstLogger
            WHERE (@sourceType IS NULL OR SourceType = @sourceType)
            ORDER BY SourceType, NamaLogger;
            """;

        try
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(HttpContext.RequestAborted);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 30;
            AddParam(cmd, "@sourceType", sourceType, DbType.String);

            await using var reader =
                await cmd.ExecuteReaderAsync(HttpContext.RequestAborted);

            var devices = new List<object>();

            while (await reader.ReadAsync(HttpContext.RequestAborted))
            {
                devices.Add(new
                {
                    loggerId = reader.GetInt32(0),
                    loggerCode = Str(reader, 1),
                    namaLogger = Str(reader, 2),
                    sourceType = Str(reader, 3),
                    lokasi = Str(reader, 4),
                    latitude = Number(reader, 5),
                    longitude = Number(reader, 6),
                    controller = Str(reader, 7),
                    communication = Str(reader, 8),
                    intervalMenit = reader.IsDBNull(9)
                        ? (int?)null : reader.GetInt32(9),
                    aktif = !reader.IsDBNull(10)
                        && reader.GetBoolean(10)
                });
            }

            return Ok(new { total = devices.Count, data = devices });
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "Gagal mengambil perangkat IoT");
            return StatusCode(503, new
            {
                message = "Gagal membaca database IoT."
            });
        }
    }

    // =====================================================
    // GET /api/iot/devices/3/latest
    // GET /api/iot/devices/3/latest?format=detail
    // =====================================================
    [HttpGet("{loggerId:int}/latest")]
    public Task<IActionResult> GetLatest(
        int loggerId,
        [FromQuery] string format = "compact")
    {
        return GetPayloads(loggerId, 1, format, true);
    }

    // =====================================================
    // GET /api/iot/devices/3/history?take=20
    // GET /api/iot/devices/3/history?take=20&format=detail
    // GET /api/iot/devices/3/history?period=day|week|month|year
    // GET /api/iot/devices/3/history?period=day|week|month|year&anchorDate=yyyy-MM-dd
    // GET /api/iot/devices/3/history?period=week&from=yyyy-MM-ddTHH:mm&to=yyyy-MM-ddTHH:mm
    // =====================================================
    [HttpGet("{loggerId:int}/history")]
    public async Task<IActionResult> GetHistory(
        int loggerId,
        [FromQuery] int take = 20,
        [FromQuery] string format = "compact",
        [FromQuery] string? period = null,
        [FromQuery] string? anchorDate = null,
        [FromQuery(Name = "from")] string? fromValue = null,
        [FromQuery(Name = "to")] string? toValue = null)
    {
        if (!string.IsNullOrWhiteSpace(period))
        {
            if (!IotHistoryPeriodDefinition.TryCreate(
                    period,
                    out var periodDefinition))
            {
                return BadRequest(new
                {
                    message = "period harus day, week, month, atau year."
                });
            }

            DateOnly? parsedAnchorDate = null;
            if (anchorDate is not null)
            {
                if (!IotHistoryPeriodDefinition.TryParseAnchorDate(
                        anchorDate,
                        out var requestedAnchorDate))
                {
                    return BadRequest(new
                    {
                        message = "anchorDate harus berformat yyyy-MM-dd."
                    });
                }

                parsedAnchorDate = requestedAnchorDate;
            }

            var hasFrom = fromValue is not null;
            var hasTo = toValue is not null;
            if (hasFrom != hasTo)
            {
                return BadRequest(new
                {
                    message = "from dan to harus diisi bersama."
                });
            }

            DateTime? parsedFrom = null;
            DateTime? parsedTo = null;
            if (hasFrom)
            {
                if (parsedAnchorDate.HasValue)
                {
                    return BadRequest(new
                    {
                        message = "Gunakan anchorDate atau from dan to."
                    });
                }

                if (!IotHistoryPeriodDefinition.TryParseRangeDateTime(
                        fromValue,
                        out var requestedFrom) ||
                    !IotHistoryPeriodDefinition.TryParseRangeDateTime(
                        toValue,
                        out var requestedTo))
                {
                    return BadRequest(new
                    {
                        message = "from dan to harus berformat yyyy-MM-ddTHH:mm."
                    });
                }

                if (requestedFrom >= requestedTo)
                {
                    return BadRequest(new
                    {
                        message = "from harus lebih kecil dari to."
                    });
                }

                parsedFrom = requestedFrom;
                parsedTo = requestedTo;
            }

            return await GetPeriodHistory(
                loggerId,
                periodDefinition,
                parsedAnchorDate,
                parsedFrom,
                parsedTo);
        }

        return await GetPayloads(loggerId, take, format, false);
    }

    // =====================================================
    // GET /api/iot/devices/3/parameters
    // Metadata parameter untuk UI.
    // =====================================================
    [HttpGet("{loggerId:int}/parameters")]
    public async Task<IActionResult> GetParameters(int loggerId)
    {
        if (!await HasAccess()) return Forbid();

        if (loggerId <= 0)
            return BadRequest(new { message = "loggerId tidak valid." });

        const string sql = """
            SELECT
                V.SensorParameterID,
                V.SensorCode,
                V.ParameterCode,
                V.NamaParameter,
                V.Satuan,
                C.CategoryCode,
                C.CategoryName,
                C.DisplayOrder
            FROM dbo.vwLoggerDataDetail AS V
            LEFT JOIN dbo.MstSensorParameter AS SP
                ON SP.SensorParameterID = V.SensorParameterID
            LEFT JOIN dbo.MstParameterCategory AS C
                ON C.ParameterCategoryID = SP.ParameterCategoryID
            WHERE V.LoggerID = @loggerId
            ORDER BY V.SensorCode, V.ParameterCode, V.SensorParameterID;
            """;

        try
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(HttpContext.RequestAborted);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 30;
            AddParam(cmd, "@loggerId", loggerId, DbType.Int32);

            await using var reader =
                await cmd.ExecuteReaderAsync(HttpContext.RequestAborted);

            var parameters = new List<object>();
            var seen = new HashSet<int>();

            while (await reader.ReadAsync(HttpContext.RequestAborted))
            {
                int id = reader.GetInt32(0);
                if (!seen.Add(id)) continue;

                parameters.Add(new
                {
                    sensorParameterId = id,
                    sensorCode = Str(reader, 1),
                    parameterCode = Str(reader, 2),
                    namaParameter = Str(reader, 3),
                    satuan = Str(reader, 4),
                    categoryCode = Str(reader, 5),
                    categoryName = Str(reader, 6),
                    categoryOrder = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7)
                });
            }

            return Ok(new { loggerId, parameters });
        }
        catch (DbException ex)
        {
            logger.LogError(ex,
                "Gagal membaca metadata LoggerID {LoggerId}", loggerId);
            return StatusCode(503, new
            {
                message = "Gagal membaca metadata sensor."
            });
        }
    }

    // =====================================================
    // PEMBACAAN PAYLOAD: SATU RawID = SATU OBJEK
    // =====================================================
    private async Task<IActionResult> GetPayloads(
        int loggerId,
        int take,
        string? format,
        bool latest)
    {
        if (!await HasAccess()) return Forbid();

        if (loggerId <= 0 || take < 1 || take > 100)
        {
            return BadRequest(new
            {
                message = "loggerId tidak valid atau take di luar 1-100."
            });
        }

        format = format?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(format))
            format = "compact";

        if (format != "compact" && format != "detail")
        {
            return BadRequest(new
            {
                message = "format harus compact atau detail."
            });
        }

        const string sql = """
            WITH Headers AS
            (
                SELECT TOP (@take)
                    H.RawID,
                    H.LoggerID,
                    H.TanggalSensor,
                    H.ReceivedAt,
                    L.LoggerCode,
                    L.SourceType
                FROM dbo.TrnLoggerRawHdr AS H
                INNER JOIN dbo.MstLogger AS L
                    ON H.LoggerID = L.LoggerID
                WHERE H.LoggerID = @loggerId
                ORDER BY H.TanggalSensor DESC, H.RawID DESC
            )
            SELECT
                H.RawID,
                H.LoggerID,
                H.LoggerCode,
                H.SourceType,
                H.TanggalSensor,
                H.ReceivedAt,

                D.RawDtlID,
                D.SensorParameterID,
                P.ParameterCode,
                P.NamaParameter,

                D.RawValue,
                D.RawText,
                D.StatusData,

                R.NilaiHasil,
                R.Satuan,
                R.StatusHasil
            FROM Headers AS H
            LEFT JOIN dbo.TrnLoggerRawDtl AS D
                ON H.RawID = D.RawID
            LEFT JOIN dbo.MstSensorParameter AS P
                ON D.SensorParameterID = P.SensorParameterID

            OUTER APPLY
            (
                SELECT TOP (1)
                    X.NilaiHasil,
                    X.Satuan,
                    X.StatusHasil
                FROM dbo.TrnLoggerHasil AS X
                WHERE X.RawDtlID = D.RawDtlID
                  AND X.IsCurrent = 1
                ORDER BY X.ProcessedAt DESC, X.HasilID DESC
            ) AS R

            ORDER BY
                H.TanggalSensor DESC,
                H.RawID DESC,
                D.RawDtlID;
            """;

        try
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(HttpContext.RequestAborted);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 60;

            AddParam(cmd, "@loggerId", loggerId, DbType.Int32);
            AddParam(cmd, "@take", take, DbType.Int32);

            var payloads = await ReadPayloads(cmd);

            if (payloads.Count == 0)
                return NotFound(new
                {
                    message = "Belum ada data untuk logger ini."
                });

            var results = payloads
                .Select(p => format == "detail"
                    ? (object)ToDetail(p)
                    : ToCompact(p))
                .ToList();

            if (latest)
                return Ok(results[0]);

            return Ok(new
            {
                loggerId,
                total = results.Count,
                data = results
            });
        }
        catch (DbException ex)
        {
            logger.LogError(ex,
                "Gagal membaca payload IoT LoggerID {LoggerId}",
                loggerId);

            return StatusCode(503, new
            {
                message = "Gagal membaca payload sensor IoT."
            });
        }
    }

    private async Task<IActionResult> GetPeriodHistory(
        int loggerId,
        IotHistoryPeriodDefinition period,
        DateOnly? anchorDate,
        DateTime? rangeFrom,
        DateTime? rangeTo)
    {
        if (!await HasAccess()) return Forbid();

        if (loggerId <= 0)
            return BadRequest(new { message = "loggerId tidak valid." });

        var window = period.Resolve(
            DateTime.Now,
            anchorDate,
            rangeFrom,
            rangeTo);

        const string sql = """
            SELECT
                H.RawID,
                H.LoggerID,
                L.LoggerCode,
                L.SourceType,
                H.TanggalSensor,
                H.ReceivedAt,

                D.RawDtlID,
                D.SensorParameterID,
                P.ParameterCode,
                P.NamaParameter,

                D.RawValue,
                D.RawText,
                D.StatusData,

                R.NilaiHasil,
                R.Satuan,
                R.StatusHasil
            FROM dbo.TrnLoggerRawHdr AS H
            INNER JOIN dbo.MstLogger AS L
                ON H.LoggerID = L.LoggerID
            LEFT JOIN dbo.TrnLoggerRawDtl AS D
                ON H.RawID = D.RawID
            LEFT JOIN dbo.MstSensorParameter AS P
                ON D.SensorParameterID = P.SensorParameterID
            OUTER APPLY
            (
                SELECT TOP (1)
                    X.NilaiHasil,
                    X.Satuan,
                    X.StatusHasil
                FROM dbo.TrnLoggerHasil AS X
                WHERE X.RawDtlID = D.RawDtlID
                  AND X.IsCurrent = 1
                ORDER BY X.ProcessedAt DESC, X.HasilID DESC
            ) AS R
            WHERE H.LoggerID = @loggerId
              AND H.TanggalSensor >= @from
              AND H.TanggalSensor < @to
            ORDER BY H.TanggalSensor, H.RawID, D.RawDtlID;
            """;

        try
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(HttpContext.RequestAborted);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 120;
            AddParam(cmd, "@loggerId", loggerId, DbType.Int32);
            AddParam(cmd, "@from", window.From, DbType.DateTime2);
            AddParam(cmd, "@to", window.To, DbType.DateTime2);

            var payloads = await ReadPayloads(cmd);
            var points = payloads
                .Select(payload => ToCompact(payload))
                .Select(payload => new IotHistoryPoint(
                    payload.RawId,
                    payload.LoggerId,
                    payload.Ts,
                    payload.ReceivedAt,
                    payload.Data))
                .ToArray();

            var data = IotHistoryAggregator
                .Aggregate(points, period)
                .Select(point => new CompactPayloadDto
                {
                    RawId = point.RawId,
                    LoggerId = point.LoggerId,
                    Ts = point.Ts,
                    ReceivedAt = point.ReceivedAt,
                    Data = point.Data.ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value,
                        StringComparer.OrdinalIgnoreCase)
                })
                .ToList();

            return Ok(new
            {
                loggerId,
                period = period.Code,
                aggregation = period.AggregationCode,
                from = window.From,
                to = window.To,
                total = data.Count,
                rawTotal = payloads.Count,
                data
            });
        }
        catch (DbException ex)
        {
            logger.LogError(ex,
                "Gagal membaca history IoT LoggerID {LoggerId} periode {Period}",
                loggerId,
                period.Code);

            return StatusCode(503, new
            {
                message = "Gagal membaca history sensor IoT."
            });
        }
    }

    private async Task<List<PayloadData>> ReadPayloads(DbCommand cmd)
    {
        await using var reader =
            await cmd.ExecuteReaderAsync(HttpContext.RequestAborted);

        var payloads = new List<PayloadData>();
        var payloadMap = new Dictionary<long, PayloadData>();

        while (await reader.ReadAsync(HttpContext.RequestAborted))
        {
            long rawId = reader.GetInt64(0);

            if (!payloadMap.TryGetValue(rawId, out var payload))
            {
                payload = new PayloadData
                {
                    RawId = rawId,
                    LoggerId = reader.GetInt32(1),
                    LoggerCode = Str(reader, 2),
                    SourceType = Str(reader, 3),
                    TanggalSensor = Date(reader, 4),
                    ReceivedAt = Date(reader, 5)
                };

                payloadMap.Add(rawId, payload);
                payloads.Add(payload);
            }

            if (reader.IsDBNull(6))
                continue;

            payload.Readings.Add(new SensorReadingDto
            {
                RawDtlId = reader.GetInt64(6),
                SensorParameterId = reader.GetInt32(7),
                ParameterCode = Str(reader, 8),
                NamaParameter = Str(reader, 9),
                RawValue = Dec(reader, 10),
                RawText = Str(reader, 11),
                StatusData = Str(reader, 12),
                NilaiHasil = Dec(reader, 13),
                Satuan = Str(reader, 14),
                StatusHasil = Str(reader, 15)
            });
        }

        return payloads;
    }

    // =====================================================
    // COMPACT: nilai hasil + error bila ada
    // =====================================================
    private static CompactPayloadDto ToCompact(PayloadData p)
    {
        var output = new CompactPayloadDto
        {
            RawId = p.RawId,
            LoggerId = p.LoggerId,
            Ts = p.TanggalSensor,
            ReceivedAt = p.ReceivedAt
        };

        // Deteksi benturan kode parameter di satu payload.
        var codeCounts = p.Readings
            .GroupBy(r => r.ParameterCode ?? "")
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var reading in p.Readings)
        {
            string code = string.IsNullOrWhiteSpace(reading.ParameterCode)
                ? "PARAMETER"
                : reading.ParameterCode;

            string key = codeCounts.GetValueOrDefault(
                reading.ParameterCode ?? "") > 1
                ? $"{code}#{reading.SensorParameterId}"
                : code;

            // Jika SensorParameterID juga berulang,
            // pertahankan tiap pembacaan memakai RawDtlID.
            if (output.Data.ContainsKey(key))
                key = $"{code}#{reading.SensorParameterId}#{reading.RawDtlId}";

            output.Data[key] = reading.NilaiHasil;

            string? error = null;

            if (reading.StatusData != "OK")
                error = reading.StatusData ?? "UNKNOWN_DATA_STATUS";
            else if (reading.NilaiHasil == null)
                error = "NO_RESULT";
            else if (reading.StatusHasil != "OK")
                error = reading.StatusHasil ?? "UNKNOWN_RESULT_STATUS";

            if (error != null)
            {
                output.Errors ??= new Dictionary<string, string>();
                output.Errors[key] = error;
            }
        }

        return output;
    }

    // =====================================================
    // DETAIL: semua atribut diagnostik
    // =====================================================
    private static DetailPayloadDto ToDetail(PayloadData p)
    {
        var output = new DetailPayloadDto
        {
            RawId = p.RawId,
            LoggerId = p.LoggerId,
            LoggerCode = p.LoggerCode,
            SourceType = p.SourceType,
            TanggalSensor = p.TanggalSensor,
            ReceivedAt = p.ReceivedAt
        };

        foreach (var reading in p.Readings)
        {
            string key = reading.SensorParameterId.ToString(
                CultureInfo.InvariantCulture);

            if (output.Sensors.ContainsKey(key))
                key = $"{key}#{reading.RawDtlId}";

            output.Sensors[key] = reading;
        }

        return output;
    }

    // =====================================================
    // HELPERS
    // =====================================================
    private static string? Str(DbDataReader r, int i) =>
        r.IsDBNull(i) ? null : r.GetString(i).Trim();

    private static decimal? Dec(DbDataReader r, int i) =>
        r.IsDBNull(i) ? null : r.GetDecimal(i);

    private static double? Number(DbDataReader r, int i)
    {
        if (r.IsDBNull(i))
            return null;

        var value = r.GetValue(i);
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

    private static DateTime? Date(DbDataReader r, int i) =>
        r.IsDBNull(i) ? null : r.GetDateTime(i);

    private static void AddParam(
        DbCommand cmd, string name, object? value, DbType type)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.DbType = type;
        param.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(param);
    }

    // =====================================================
    // INTERNAL DATA
    // =====================================================
    private sealed class PayloadData
    {
        public long RawId { get; set; }
        public int LoggerId { get; set; }
        public string? LoggerCode { get; set; }
        public string? SourceType { get; set; }
        public DateTime? TanggalSensor { get; set; }
        public DateTime? ReceivedAt { get; set; }

        public List<SensorReadingDto> Readings { get; } = new();
    }

    // =====================================================
    // RESPONSE DTO
    // =====================================================
    public sealed class CompactPayloadDto
    {
        public long RawId { get; set; }
        public int LoggerId { get; set; }
        public DateTime? Ts { get; set; }
        public DateTime? ReceivedAt { get; set; }

        public Dictionary<string, decimal?> Data { get; set; } = new();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string>? Errors { get; set; }
    }

    public sealed class DetailPayloadDto
    {
        public long RawId { get; set; }
        public int LoggerId { get; set; }
        public string? LoggerCode { get; set; }
        public string? SourceType { get; set; }
        public DateTime? TanggalSensor { get; set; }
        public DateTime? ReceivedAt { get; set; }

        public Dictionary<string, SensorReadingDto> Sensors
            { get; set; } = new();
    }

    public sealed class SensorReadingDto
    {
        public long RawDtlId { get; set; }
        public int SensorParameterId { get; set; }
        public string? ParameterCode { get; set; }
        public string? NamaParameter { get; set; }
        public decimal? RawValue { get; set; }
        public string? RawText { get; set; }
        public string? StatusData { get; set; }
        public decimal? NilaiHasil { get; set; }
        public string? Satuan { get; set; }
        public string? StatusHasil { get; set; }
    }
}
