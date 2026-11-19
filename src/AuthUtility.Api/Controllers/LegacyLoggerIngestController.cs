using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

// Endpoint ingest untuk logger legacy: menerima payload yang sama dengan
// ECS_Logger::SaveData lalu menyimpannya ke database MyKTTATest.
// Autentikasi memakai header X-Ingest-Key, nilainya dari
// konfigurasi "LegacyIngest:ApiKey". Tanpa konfigurasi, endpoint menolak.
[ApiController]
[Route("api/iot/legacy")]
public class LegacyLoggerIngestController(
    MyKTTATestDbContext db,
    IConfiguration configuration,
    ILogger<LegacyLoggerIngestController> logger
) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [HttpPost("logger-data")]
    [AllowAnonymous]
    public async Task<IActionResult> SaveLoggerData(CancellationToken ct)
    {
        var keyError = ValidateIngestKey();
        if (keyError is not null) return keyError;

        string body;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(body))
            return BadRequest(new { status = "failed", message = "Body request kosong atau tidak valid" });

        LegacySaveRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<LegacySaveRequest>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return BadRequest(new { status = "failed", message = "Body request bukan JSON yang valid" });
        }

        if (request is null)
            return BadRequest(new { status = "failed", message = "Body request kosong atau tidak valid" });

        var loggerCode = request.Logger?.Code?.Trim();
        if (string.IsNullOrEmpty(loggerCode))
            return BadRequest(new { status = "failed", message = "Field logger.code wajib diisi" });

        if (string.IsNullOrWhiteSpace(request.Ts))
            return BadRequest(new { status = "failed", message = "Field ts wajib diisi" });

        if (request.Data is null || request.Data.Count == 0)
            return BadRequest(new { status = "failed", message = "Field data sensor wajib diisi" });

        if (!TryParseSensorTime(request.Ts, out var sensorTime))
            return BadRequest(new
            {
                status = "failed",
                message = "Field ts harus berformat YYYY-MM-DD HH:MM:SS"
            });

        DbConnection? connection = null;
        DbTransaction? transaction = null;

        try
        {
            connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(ct);

            var loggerRow = await FindActiveLogger(connection, loggerCode, ct);
            if (loggerRow is null)
                return NotFound(new
                {
                    status = "failed",
                    message = "Logger tidak ditemukan atau tidak aktif",
                    logger = loggerCode
                });

            transaction = await connection.BeginTransactionAsync(ct);

            var header = await UpsertRawHeader(
                connection,
                transaction,
                loggerRow.Value.LoggerId,
                sensorTime,
                Truncate(request.Ket, 100),
                body,
                ct);

            var rawId = header.RawId;

            var jumlahRaw = 0;
            var jumlahHasil = 0;
            var unknownParameters = new List<string>();

            foreach (var (parameterCodeRaw, rawValue) in request.Data)
            {
                var parameterCode = parameterCodeRaw.Trim();
                if (parameterCode.Length == 0) continue;

                var parameter = await FindSensorParameter(
                    connection,
                    transaction,
                    loggerRow.Value.LoggerId,
                    parameterCode,
                    ct);

                if (parameter is null)
                {
                    unknownParameters.Add(parameterCode);
                    continue;
                }

                var statusData = Truncate(
                    ReadStatus(request.Status, parameterCode),
                    30) ?? "UNKNOWN";

                var (rawNumeric, rawText) = ParseRawValue(rawValue);

                var rawDtlId = await UpsertRawDetail(
                    connection,
                    transaction,
                    rawId,
                    parameter.Value.SensorParameterId,
                    rawNumeric,
                    Truncate(rawText, 250),
                    statusData,
                    ct);

                jumlahRaw++;

                // Nilai non-numerik tetap disimpan sebagai raw, tanpa hasil.
                if (rawNumeric is null) continue;

                var calibration = await FindCalibration(
                    connection,
                    transaction,
                    parameter.Value.SensorParameterId,
                    sensorTime,
                    ct);

                if (calibration is null) continue;

                var calibrationParameters = await ReadCalibrationParameters(
                    connection,
                    transaction,
                    calibration.Value.KalibrasiId,
                    ct);

                var nilaiHasil = ComputeResult(rawNumeric.Value, calibrationParameters);

                await UpsertResult(
                    connection,
                    transaction,
                    rawDtlId,
                    calibration.Value.KalibrasiId,
                    nilaiHasil,
                    Truncate(parameter.Value.OutputUnit, 20),
                    statusData,
                    ct);

                jumlahHasil++;
            }

            await UpsertLoggerStatus(
                connection,
                transaction,
                loggerRow.Value.LoggerId,
                sensorTime,
                request.Device,
                Truncate(request.Ket, 250),
                ct);

            await transaction.CommitAsync(ct);

            var action = header.Action;

            return Ok(new
            {
                status = "success",
                action,
                message = action == "updated"
                    ? "Payload DataLogger berhasil diperbarui"
                    : "Payload DataLogger berhasil diproses",
                RawID = rawId,
                LoggerCode = loggerCode,
                TanggalSensor = sensorTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                JumlahRaw = jumlahRaw,
                JumlahHasil = jumlahHasil,
                UnknownParameters = unknownParameters
            });
        }
        catch (Exception ex)
        {
            if (transaction is not null)
            {
                try { await transaction.RollbackAsync(ct); }
                catch (Exception rollbackEx) { logger.LogWarning(rollbackEx, "Rollback ingest legacy gagal."); }
            }

            logger.LogError(ex, "Ingest legacy logger {LoggerCode} gagal.", loggerCode);

            return StatusCode(500, new { status = "error", message = ex.Message });
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private IActionResult? ValidateIngestKey()
    {
        var expected = configuration["LegacyIngest:ApiKey"];
        if (string.IsNullOrWhiteSpace(expected))
        {
            logger.LogError("LegacyIngest:ApiKey belum dikonfigurasi; ingest legacy ditolak.");
            return StatusCode(503, new
            {
                status = "failed",
                message = "Ingest legacy belum dikonfigurasi di server (LegacyIngest:ApiKey)."
            });
        }

        var provided = Request.Headers["X-Ingest-Key"].ToString();
        if (string.IsNullOrEmpty(provided))
            return Unauthorized(new { status = "failed", message = "Header X-Ingest-Key wajib diisi" });

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);

        var matches = expectedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);

        return matches
            ? null
            : Unauthorized(new { status = "failed", message = "X-Ingest-Key tidak valid" });
    }

    private static bool TryParseSensorTime(string value, out DateTime result)
    {
        string[] formats =
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd'T'HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.FFFFFFF",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF"
        };

        return DateTime.TryParseExact(
            value.Trim(),
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out result);
    }

    private static async Task<(int LoggerId, string LoggerCode)?> FindActiveLogger(
        DbConnection connection,
        string loggerCode,
        CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT TOP (1) LoggerID, LoggerCode
            FROM dbo.MstLogger
            WHERE LoggerCode = @LoggerCode AND Aktif = 1
            """;
        AddParam(cmd, "@LoggerCode", loggerCode);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return (reader.GetInt32(0), reader.GetString(1));
    }

    private static async Task<(long RawId, string Action)> UpsertRawHeader(
        DbConnection connection,
        DbTransaction transaction,
        int loggerId,
        DateTime sensorTime,
        string? ket,
        string payloadRaw,
        CancellationToken ct)
    {
        await using (var find = connection.CreateCommand())
        {
            find.Transaction = transaction;
            find.CommandText = """
                SELECT TOP (1) RawID
                FROM dbo.TrnLoggerRawHdr
                WHERE LoggerID = @LoggerID AND TanggalSensor = @TanggalSensor
                """;
            AddParam(find, "@LoggerID", loggerId);
            AddParam(find, "@TanggalSensor", sensorTime);

            var existing = await find.ExecuteScalarAsync(ct);
            if (existing is not null && existing is not DBNull)
            {
                var rawId = Convert.ToInt64(existing, CultureInfo.InvariantCulture);

                await using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE dbo.TrnLoggerRawHdr
                    SET Ket = @Ket, PayloadRaw = @PayloadRaw
                    WHERE RawID = @RawID
                    """;
                AddParam(update, "@RawID", rawId);
                AddParam(update, "@Ket", ket);
                AddParam(update, "@PayloadRaw", payloadRaw);
                await update.ExecuteNonQueryAsync(ct);

                return (rawId, "updated");
            }
        }

        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO dbo.TrnLoggerRawHdr
                (LoggerID, TanggalSensor, ReceivedAt, Ket, PayloadRaw)
            OUTPUT INSERTED.RawID
            VALUES
                (@LoggerID, @TanggalSensor, SYSDATETIME(), @Ket, @PayloadRaw)
            """;
        AddParam(insert, "@LoggerID", loggerId);
        AddParam(insert, "@TanggalSensor", sensorTime);
        AddParam(insert, "@Ket", ket);
        AddParam(insert, "@PayloadRaw", payloadRaw);

        var inserted = await insert.ExecuteScalarAsync(ct);
        if (inserted is null || inserted is DBNull)
            throw new InvalidOperationException("RawID gagal diperoleh setelah INSERT TrnLoggerRawHdr");

        return (Convert.ToInt64(inserted, CultureInfo.InvariantCulture), "inserted");
    }

    private static async Task<(int SensorParameterId, string? OutputUnit)?> FindSensorParameter(
        DbConnection connection,
        DbTransaction transaction,
        int loggerId,
        string parameterCode,
        CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            SELECT TOP (1) SP.SensorParameterID, SP.OutputUnit
            FROM dbo.MstSensorParameter AS SP
            INNER JOIN dbo.MstSensor AS S ON S.SensorID = SP.SensorID
            WHERE S.LoggerID = @LoggerID
              AND SP.ParameterCode = @ParameterCode
              AND S.Aktif = 1
              AND SP.Aktif = 1
            """;
        AddParam(cmd, "@LoggerID", loggerId);
        AddParam(cmd, "@ParameterCode", parameterCode);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return (
            reader.GetInt32(0),
            reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    private static async Task<long> UpsertRawDetail(
        DbConnection connection,
        DbTransaction transaction,
        long rawId,
        int sensorParameterId,
        decimal? rawNumeric,
        string? rawText,
        string statusData,
        CancellationToken ct)
    {
        await using (var find = connection.CreateCommand())
        {
            find.Transaction = transaction;
            find.CommandText = """
                SELECT TOP (1) RawDtlID
                FROM dbo.TrnLoggerRawDtl
                WHERE RawID = @RawID AND SensorParameterID = @SensorParameterID
                ORDER BY RawDtlID DESC
                """;
            AddParam(find, "@RawID", rawId);
            AddParam(find, "@SensorParameterID", sensorParameterId);

            var existing = await find.ExecuteScalarAsync(ct);
            if (existing is not null && existing is not DBNull)
            {
                var rawDtlId = Convert.ToInt64(existing, CultureInfo.InvariantCulture);

                await using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE dbo.TrnLoggerRawDtl
                    SET RawValue = @RawValue, RawText = @RawText, StatusData = @StatusData
                    WHERE RawDtlID = @RawDtlID
                    """;
                AddParam(update, "@RawDtlID", rawDtlId);
                AddParam(update, "@RawValue", rawNumeric);
                AddParam(update, "@RawText", rawText);
                AddParam(update, "@StatusData", statusData);
                await update.ExecuteNonQueryAsync(ct);

                return rawDtlId;
            }
        }

        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO dbo.TrnLoggerRawDtl
                (RawID, SensorParameterID, RawValue, RawText, StatusData, CreatedDate)
            OUTPUT INSERTED.RawDtlID
            VALUES
                (@RawID, @SensorParameterID, @RawValue, @RawText, @StatusData, SYSDATETIME())
            """;
        AddParam(insert, "@RawID", rawId);
        AddParam(insert, "@SensorParameterID", sensorParameterId);
        AddParam(insert, "@RawValue", rawNumeric);
        AddParam(insert, "@RawText", rawText);
        AddParam(insert, "@StatusData", statusData);

        var inserted = await insert.ExecuteScalarAsync(ct);
        if (inserted is null || inserted is DBNull)
            throw new InvalidOperationException(
                $"RawDtlID gagal diperoleh untuk SensorParameterID {sensorParameterId}");

        return Convert.ToInt64(inserted, CultureInfo.InvariantCulture);
    }

    private static async Task<(long KalibrasiId, int Versi)?> FindCalibration(
        DbConnection connection,
        DbTransaction transaction,
        int sensorParameterId,
        DateTime sensorTime,
        CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            SELECT TOP (1) KalibrasiID, Versi
            FROM dbo.MstKalibrasi
            WHERE SensorParameterID = @SensorParameterID
              AND BerlakuMulai <= @TanggalSensor
              AND (BerlakuSampai IS NULL OR BerlakuSampai >= @TanggalSensor)
            ORDER BY Versi DESC
            """;
        AddParam(cmd, "@SensorParameterID", sensorParameterId);
        AddParam(cmd, "@TanggalSensor", sensorTime);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return (reader.GetInt64(0), reader.GetInt32(1));
    }

    private static async Task<Dictionary<string, decimal>> ReadCalibrationParameters(
        DbConnection connection,
        DbTransaction transaction,
        long kalibrasiId,
        CancellationToken ct)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            SELECT NamaParameter, NilaiParameter
            FROM dbo.MstKalibrasiParameter
            WHERE KalibrasiID = @KalibrasiID
            """;
        AddParam(cmd, "@KalibrasiID", kalibrasiId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var name = reader.IsDBNull(0) ? null : reader.GetString(0);
            if (string.IsNullOrWhiteSpace(name)) continue;

            result[name.Trim().ToUpperInvariant()] = reader.GetDecimal(1);
        }

        return result;
    }

    private static decimal ComputeResult(decimal rawValue, Dictionary<string, decimal> parameters)
    {
        if (parameters.TryGetValue("TIP_FACTOR", out var tipFactor))
            return rawValue * tipFactor;

        var scale = parameters.TryGetValue("SCALE", out var s) ? s : 1m;
        var offset = parameters.TryGetValue("OFFSET", out var o) ? o : 0m;

        return rawValue * scale + offset;
    }

    private static async Task UpsertResult(
        DbConnection connection,
        DbTransaction transaction,
        long rawDtlId,
        long kalibrasiId,
        decimal nilaiHasil,
        string? satuan,
        string statusHasil,
        CancellationToken ct)
    {
        await using (var find = connection.CreateCommand())
        {
            find.Transaction = transaction;
            find.CommandText = """
                SELECT TOP (1) HasilID
                FROM dbo.TrnLoggerHasil
                WHERE RawDtlID = @RawDtlID AND IsCurrent = 1
                ORDER BY HasilID DESC
                """;
            AddParam(find, "@RawDtlID", rawDtlId);

            var existing = await find.ExecuteScalarAsync(ct);
            if (existing is not null && existing is not DBNull)
            {
                await using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE dbo.TrnLoggerHasil
                    SET KalibrasiID = @KalibrasiID,
                        NilaiHasil = @NilaiHasil,
                        Satuan = @Satuan,
                        VersiProses = 1,
                        IsCurrent = 1,
                        StatusHasil = @StatusHasil,
                        ProcessedAt = SYSDATETIME()
                    WHERE HasilID = @HasilID
                    """;
                AddParam(update, "@HasilID", Convert.ToInt64(existing, CultureInfo.InvariantCulture));
                AddParam(update, "@KalibrasiID", kalibrasiId);
                AddParam(update, "@NilaiHasil", nilaiHasil);
                AddParam(update, "@Satuan", satuan);
                AddParam(update, "@StatusHasil", statusHasil);
                await update.ExecuteNonQueryAsync(ct);

                return;
            }
        }

        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO dbo.TrnLoggerHasil
                (RawDtlID, KalibrasiID, NilaiHasil, Satuan, VersiProses, IsCurrent, StatusHasil, ProcessedAt)
            VALUES
                (@RawDtlID, @KalibrasiID, @NilaiHasil, @Satuan, 1, 1, @StatusHasil, SYSDATETIME())
            """;
        AddParam(insert, "@RawDtlID", rawDtlId);
        AddParam(insert, "@KalibrasiID", kalibrasiId);
        AddParam(insert, "@NilaiHasil", nilaiHasil);
        AddParam(insert, "@Satuan", satuan);
        AddParam(insert, "@StatusHasil", statusHasil);
        await insert.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpsertLoggerStatus(
        DbConnection connection,
        DbTransaction transaction,
        int loggerId,
        DateTime sensorTime,
        LegacyDeviceInfo? device,
        string? ket,
        CancellationToken ct)
    {
        await using (var find = connection.CreateCommand())
        {
            find.Transaction = transaction;
            find.CommandText = "SELECT TOP (1) StatusID FROM dbo.TrnLoggerStatus WHERE LoggerID = @LoggerID";
            AddParam(find, "@LoggerID", loggerId);

            var existing = await find.ExecuteScalarAsync(ct);
            if (existing is not null && existing is not DBNull)
            {
                await using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE dbo.TrnLoggerStatus
                    SET Tanggal = @Tanggal,
                        RSSI = @RSSI,
                        WiFiConnected = @WiFiConnected,
                        MQTTConnected = @MQTTConnected,
                        UptimeSec = @UptimeSec,
                        FreeHeap = @FreeHeap,
                        Ket = @Ket
                    WHERE LoggerID = @LoggerID
                    """;
                AddParam(update, "@LoggerID", loggerId);
                AddStatusParameters(update, sensorTime, device, ket);
                await update.ExecuteNonQueryAsync(ct);

                return;
            }
        }

        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO dbo.TrnLoggerStatus
                (LoggerID, Tanggal, RSSI, WiFiConnected, MQTTConnected, UptimeSec, FreeHeap, Ket)
            VALUES
                (@LoggerID, @Tanggal, @RSSI, @WiFiConnected, @MQTTConnected, @UptimeSec, @FreeHeap, @Ket)
            """;
        AddParam(insert, "@LoggerID", loggerId);
        AddStatusParameters(insert, sensorTime, device, ket);
        await insert.ExecuteNonQueryAsync(ct);
    }

    private static void AddStatusParameters(
        DbCommand cmd,
        DateTime sensorTime,
        LegacyDeviceInfo? device,
        string? ket)
    {
        AddParam(cmd, "@Tanggal", sensorTime);
        AddParam(cmd, "@RSSI", device?.Rssi);
        AddParam(cmd, "@WiFiConnected", device?.Wifi);
        AddParam(cmd, "@MQTTConnected", device?.Mqtt);
        AddParam(cmd, "@UptimeSec", device?.Uptime);
        AddParam(cmd, "@FreeHeap", device?.FreeHeap);
        AddParam(cmd, "@Ket", ket);
    }

    private static string? ReadStatus(Dictionary<string, JsonElement>? status, string parameterCode)
    {
        if (status is null) return null;

        foreach (var (key, value) in status)
        {
            if (!string.Equals(key.Trim(), parameterCode, StringComparison.OrdinalIgnoreCase))
                continue;

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => value.GetRawText()
            };
        }

        return null;
    }

    private static (decimal? Numeric, string? Text) ParseRawValue(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                return value.TryGetDecimal(out var numeric)
                    ? (numeric, null)
                    : (null, value.GetRawText());

            case JsonValueKind.String:
                var text = value.GetString();
                if (text is not null
                    && decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    return (parsed, null);
                }

                return (null, text);

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return (null, null);

            default:
                return (null, value.GetRawText());
        }
    }

    private static void AddParam(DbCommand cmd, string name, object? value)
    {
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(parameter);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed class LegacySaveRequest
    {
        public LegacyLoggerRef? Logger { get; set; }
        public string? Ts { get; set; }
        public Dictionary<string, JsonElement>? Data { get; set; }
        public Dictionary<string, JsonElement>? Status { get; set; }
        public LegacyDeviceInfo? Device { get; set; }
        public string? Ket { get; set; }
    }

    private sealed class LegacyLoggerRef
    {
        public string? Code { get; set; }
    }

    private sealed class LegacyDeviceInfo
    {
        public int? Rssi { get; set; }
        public bool? Wifi { get; set; }
        public bool? Mqtt { get; set; }
        public long? Uptime { get; set; }
        public long? FreeHeap { get; set; }
    }
}
