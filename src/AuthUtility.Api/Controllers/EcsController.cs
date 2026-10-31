using System.Data;
using System.Data.Common;
using System.Globalization;
using AuthUtility.Application.Interfaces;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

// ECS Monitoring (akses WAD): salinan endpoint GET dari APIMyECS (PHP, source/ECS_CurahHujan.php & ECS_Tmak.php),
// dibaca langsung dari database MyKTTATest. HANYA SELECT - endpoint SaveData* (INSERT) sengaja tidak disalin.
// Akses = Read pada salah satu menu WAD_ECS_* (lihat frontend features/ecs/ecsConfig.tsx).
// Kolom dikembalikan dengan nama asli database agar sama dengan JSON dari APIMyECS.
[ApiController]
[Route("api/ecs")]
[Authorize]
public class EcsController(
    MyKTTATestDbContext db,
    IConfiguration configuration,
    IPermissionService permissions,
    ILogger<EcsController> logger
) : ControllerBase
{
    public static readonly string[] MenuCodes =
    {
        "WAD_ECS_DASHBOARD", "WAD_ECS_STASIUN", "WAD_ECS_MONITOR_CH", "WAD_ECS_DATA_HISTORIS", "WAD_ECS_KALENDER_CH",
        "WAD_ECS_PETA", "WAD_ECS_CURAH_HUJAN", "WAD_ECS_TMAK", "WAD_ECS_KELEMBAPAN",
    };

    // GET api/ecs/curah-hujan/2026-10-01/2026-10-14
    // = ECS_CurahHujan/GetMonitorCurahHujan/{date1}/{date2} (data logger per stasiun & sensor, VwLoggerTest).
    [HttpGet("curah-hujan/{date1}/{date2}")]
    public Task<IActionResult> GetCurahHujan(string date1, string date2) =>
        Query(date1, date2, @"
            SELECT *
            FROM dbo.VwLoggerTest AS a
            WHERE a.TanggalSensor >= @date1
              AND a.TanggalSensor < DATEADD(DAY, 1, @date2)
            ORDER BY a.LoggerID, a.SensorParameterID, a.TanggalSensor");

    // GET api/ecs/tmak/2026-10-01/2026-10-14
    // = ECS_Tmak/GetMonTmakNew/{date1}/{date2}/{userid}; user ECS dari appsettings Ecs:UserId.
    [HttpGet("tmak/{date1}/{date2}")]
    public Task<IActionResult> GetTmak(string date1, string date2) =>
        Query(date1, date2, @"
            SELECT Tanggal, KodeBlok, Nilai, KoordinatX, KoordinatY, ket
            FROM vw_ECSMonTmak
            WHERE CONVERT(DATE, Tanggal) BETWEEN @date1 AND @date2
              AND UserID = @userid
            ORDER BY Tanggal",
            ("@userid", configuration["Ecs:UserId"] ?? ""));

    // GET api/ecs/kelembapan/2026-10-01/2026-10-14
    // = ECS_CurahHujan/GetMonKelembapan/{date1}/{date2}.
    [HttpGet("kelembapan/{date1}/{date2}")]
    public Task<IActionResult> GetKelembapan(string date1, string date2) =>
        Query(date1, date2, @"
            SELECT
                Tanggal,
                LokasiCurahHujan     AS Lokasi,
                NilaiKelembapanTanah AS NilaiKelembapan,
                IdSensor
            FROM tblCurahHujanTest
            WHERE NilaiKelembapanTanah IS NOT NULL
              AND CONVERT(DATE, Tanggal) BETWEEN @date1 AND @date2
            ORDER BY Tanggal");

    /* =========================================================
       HELPER
    ========================================================= */

    private async Task<IActionResult> Query(string date1, string date2, string sql, params (string Name, object Value)[] extra)
    {
        if (!await HasAccess()) return Forbid();

        if (!TryDate(date1, out var from) || !TryDate(date2, out var to))
            return BadRequest(new { message = "Format tanggal harus yyyy-MM-dd." });

        try
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) await connection.OpenAsync(HttpContext.RequestAborted);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 120;
            AddParameter(command, "@date1", from, DbType.Date);
            AddParameter(command, "@date2", to, DbType.Date);
            foreach (var (name, value) in extra) AddParameter(command, name, value, DbType.String);

            await using var reader = await command.ExecuteReaderAsync(HttpContext.RequestAborted);
            var rows = new List<Dictionary<string, object?>>();

            while (await reader.ReadAsync(HttpContext.RequestAborted))
            {
                var row = new Dictionary<string, object?>(reader.FieldCount);
                for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = Value(reader.GetValue(i));
                rows.Add(row);
            }

            return Ok(rows);
        }
        catch (DbException error)
        {
            logger.LogError(error, "Gagal membaca data ECS dari MyKTTATest");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Gagal membaca data dari database MyKTTATest. Coba lagi beberapa saat." });
        }
    }

    private async Task<bool> HasAccess()
    {
        foreach (var code in MenuCodes)
        {
            if (await permissions.HasPermissionAsync(User, code, MenuPermission.Read)) return true;
        }

        return false;
    }

    private static void AddParameter(DbCommand command, string name, object value, DbType type)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static bool TryDate(string value, out DateTime date)
    {
        var ok = DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d);
        date = ok ? d.ToDateTime(TimeOnly.MinValue) : default;
        return ok;
    }

    // Tanggal ditulis seperti PDO sqlsrv di APIMyECS ("2026-10-14 08:05:00.000"); biner dilewati.
    private static object? Value(object value) => value switch
    {
        DBNull => null,
        DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
        DateTimeOffset d => d.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
        byte[] => null,
        string s => s.Trim(),
        _ => value,
    };
}
