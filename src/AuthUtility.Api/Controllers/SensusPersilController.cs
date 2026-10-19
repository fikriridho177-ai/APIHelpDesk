using System.Data.Common;
using AuthUtility.Api.Sensus;
using AuthUtility.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthUtility.Api.Controllers;

// Master Kebun & Blok: data persil dibaca langsung dari MyPAD & RSUPPKB (lihat PersilReader). HANYA GET.
// Akses = Read pada menu SNS_MASTER_LOKASI.
[ApiController]
[Route("api/sensus/persil")]
[Authorize]
public class SensusPersilController(
    PersilReader reader,
    IPermissionService permissions,
    ILogger<SensusPersilController> logger
) : ControllerBase
{
    public record PersilResultDto(string Sumber, int Total, bool Terpotong, List<PersilReader.Persil> Rows);

    // GET api/sensus/persil?sumber=KLP&wilayah=W01&search=abc
    [HttpGet]
    public async Task<IActionResult> Get(string sumber, string? wilayah, string? search)
    {
        if (!await CanRead()) return Forbid();
        var source = PersilReader.Find(sumber);
        if (source == null) return BadRequest(new { message = "Sumber data tidak dikenal." });

        try
        {
            var rows = await reader.ListAsync(source, wilayah, search, withDetail: true);
            var terpotong = rows.Count > PersilReader.MaxRows;
            if (terpotong) rows.RemoveAt(rows.Count - 1);

            return Ok(new PersilResultDto(source.Key, rows.Count, terpotong, rows));
        }
        catch (DbException error)
        {
            return SourceError(logger, source, error);
        }
    }

    // GET api/sensus/persil/wilayah?sumber=KLP  -> daftar KodeWilayah untuk filter.
    [HttpGet("wilayah")]
    public async Task<IActionResult> GetWilayah(string sumber)
    {
        if (!await CanRead()) return Forbid();
        var source = PersilReader.Find(sumber);
        if (source == null) return BadRequest(new { message = "Sumber data tidak dikenal." });

        try
        {
            return Ok(await reader.WilayahAsync(source));
        }
        catch (DbException error)
        {
            return SourceError(logger, source, error);
        }
    }

    private Task<bool> CanRead() => permissions.HasPermissionAsync(User, SnsMenu.MasterLokasi, MenuPermission.Read);

    public static ObjectResult SourceError(ILogger logger, PersilReader.Source source, DbException error)
    {
        logger.LogError(error, "Gagal membaca {Table} dari {Database}", source.Table, source.Database);
        return new ObjectResult(new { message = $"Gagal membaca data dari database {source.Database}. Coba lagi beberapa saat." })
        {
            StatusCode = StatusCodes.Status503ServiceUnavailable
        };
    }
}
