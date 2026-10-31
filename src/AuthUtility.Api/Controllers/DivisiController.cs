using AuthUtility.Api.Sensus;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

// Data ringkasan untuk halaman divisi (/divisi/rna, /divisi/gis).
// GIS memakai api/layanan/report (tiket per jenis permintaan), jadi di sini hanya RNA.
[ApiController]
[Route("api/divisi")]
[Authorize]
public class DivisiController(AppDbContext db, IPermissionService permissions) : ControllerBase
{
    public const string RnaMenuCode = "LYN_RNA";

    // GET api/divisi/rna/ringkasan?hari=90
    // Per jenis sensus (MstHpt): jumlah sensus, menunggu approval, dan persil berat / sedang / ringan / nihil.
    [HttpGet("rna/ringkasan")]
    public async Task<IActionResult> GetRnaRingkasan(int hari = 90)
    {
        if (!await permissions.HasPermissionAsync(User, RnaMenuCode, MenuPermission.View) &&
            !await permissions.HasPermissionAsync(User, SnsMenu.Monitoring, MenuPermission.Read) &&
            !await permissions.HasPermissionAsync(User, SnsMenu.Analisis, MenuPermission.Read))
            return Forbid();

        hari = Math.Clamp(hari, 7, 366);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var dateFrom = today.AddDays(-(hari - 1));

        var jenisList = await db.SnsJenis.AsNoTracking()
            .Where(j => j.Active)
            .Include(j => j.Parameters)
            .OrderBy(j => j.Id)
            .ToListAsync();

        // Indikator utama = parameter indikator aktif dengan Id terkecil (sama dengan halaman Analisis).
        var indikator = jenisList.ToDictionary(
            j => j.Id,
            j => j.Parameters.Where(p => p.IsIndikator && p.Active).OrderBy(p => p.Id).FirstOrDefault());

        var headers = await db.SnsHeaders.AsNoTracking()
            .Where(h => h.TanggalSensus >= dateFrom && h.TanggalSensus <= today)
            .Select(h => new RnaHeaderRow(h.Id, h.JenisId, h.Status, h.TanggalSensus, h.PersilSumber, h.PersilId))
            .ToListAsync();

        var parameterIds = indikator.Values.Where(p => p != null).Select(p => p!.Id).ToList();
        var approvedIds = headers.Where(h => h.Status == SnsStatus.Approved).Select(h => h.Id).ToList();

        var nilai = parameterIds.Count == 0 || approvedIds.Count == 0
            ? new Dictionary<long, decimal?>()
            : await db.SnsDetails.AsNoTracking()
                .Where(d => approvedIds.Contains(d.HeaderId) && parameterIds.Contains(d.ParameterId) && d.NilaiAngka != null)
                .GroupBy(d => d.HeaderId)
                .Select(g => new { g.Key, Rata = g.Average(x => x.NilaiAngka) })
                .ToDictionaryAsync(x => x.Key, x => x.Rata);

        var info = jenisList.Select(j =>
        {
            var p = indikator[j.Id];
            return new RnaJenisInfo(j.Id, j.Code, j.Name, j.Kategori, j.Komoditas, j.Description,
                p?.AmbangSedang, p?.AmbangBerat, p != null);
        });

        return Ok(new
        {
            dateFrom,
            dateTo = today,
            jenis = RnaRingkasanCalculator.Build(info, headers, nilai),
        });
    }
}
