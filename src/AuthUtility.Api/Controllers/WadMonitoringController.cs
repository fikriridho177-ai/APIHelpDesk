using System.Data;
using System.Data.Common;
using System.Globalization;
using AuthUtility.Api.Wad;
using AuthUtility.Application.Interfaces;
using AuthUtility.Application.Security;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

[ApiController]
[Route("api/wad/monitoring")]
[Authorize]
public class WadMonitoringController(
    MyKTTATestDbContext db,
    IConfiguration configuration,
    IPermissionService permissions,
    ILogger<WadMonitoringController> logger
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? parameter,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        [FromQuery] string? wilayah,
        [FromQuery] string? pos,
        [FromQuery] bool isLatestOnly = false)
    {
        if (!await HasAccess()) return Forbid();

        if (!WadMonitoringRules.TryGetSource(parameter, out var source))
            return BadRequest(new { message = "Parameter monitoring harus curah-hujan, tmak, atau tmat." });

        var hasStartDate = !string.IsNullOrWhiteSpace(startDate);
        var hasEndDate = !string.IsNullOrWhiteSpace(endDate);
        var from = default(DateTime);
        var to = default(DateTime);
        if (hasStartDate && !TryDate(startDate!, out from))
            return BadRequest(new { message = "Format startDate harus yyyy-MM-dd." });
        if (hasEndDate && !TryDate(endDate!, out to))
            return BadRequest(new { message = "Format endDate harus yyyy-MM-dd." });

        if (hasStartDate && hasEndDate && from > to)
            return BadRequest(new { message = "startDate tidak boleh melewati endDate." });

        var hasWilayah = !string.IsNullOrWhiteSpace(wilayah);
        var hasPos = !string.IsNullOrWhiteSpace(pos);
        if (hasWilayah && source.RegionColumn is null)
            return BadRequest(new { message = $"Filter wilayah belum tersedia pada view {source.ViewName}." });

        var onlineWindowMinutes = Math.Clamp(
            configuration.GetValue<int?>("WadMonitoring:OnlineWindowMinutes") ?? 15,
            1,
            1440);

        try
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(HttpContext.RequestAborted);

            await using var command = connection.CreateCommand();
            command.CommandText = WadMonitoringRules.BuildQuery(
                source,
                hasStartDate,
                hasEndDate,
                hasWilayah,
                hasPos,
                isLatestOnly);
            command.CommandTimeout = 120;

            if (hasStartDate) AddParameter(command, "@startDate", from, DbType.Date);
            if (hasEndDate) AddParameter(command, "@endDate", to, DbType.Date);
            if (hasWilayah) AddParameter(command, "@wilayah", wilayah!.Trim(), DbType.String);
            if (hasPos) AddParameter(command, "@pos", pos!.Trim(), DbType.String);
            AddParameter(command, "@onlineWindowMinutes", onlineWindowMinutes, DbType.Int32);

            await using var reader = await command.ExecuteReaderAsync(HttpContext.RequestAborted);
            var rows = new List<Dictionary<string, object?>>();

            while (await reader.ReadAsync(HttpContext.RequestAborted))
            {
                var row = new Dictionary<string, object?>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = Value(reader.GetValue(i));
                rows.Add(row);
            }

            var online = rows.Count(row => row.TryGetValue("isOnline", out var value) && value is true);
            var koordinatValid = rows.Count(row =>
                IsFiniteNumber(row.GetValueOrDefault("koordinatX"))
                && IsFiniteNumber(row.GetValueOrDefault("koordinatY")));

            return Ok(new
            {
                data = rows,
                meta = new
                {
                    parameter = source.Parameter,
                    total = rows.Count,
                    online,
                    koordinatValid,
                },
            });
        }
        catch (DbException error)
        {
            logger.LogError(error, "Gagal membaca view monitoring WAD dari MyKTTATest");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Gagal membaca data monitoring dari database MyKTTATest. Coba lagi beberapa saat." });
        }
    }

    private async Task<bool> HasAccess()
    {
        foreach (var code in EcsController.MenuCodes)
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
        var ok = DateOnly.TryParseExact(
            value.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed);
        date = ok ? parsed.ToDateTime(TimeOnly.MinValue) : default;
        return ok;
    }

    private static bool IsFiniteNumber(object? value)
    {
        if (value is null || value is DBNull) return false;
        return double.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var number) && double.IsFinite(number);
    }

    private static object? Value(object value) => value switch
    {
        DBNull => null,
        DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
        byte[] => null,
        string text => text.Trim(),
        _ => value,
    };
}
