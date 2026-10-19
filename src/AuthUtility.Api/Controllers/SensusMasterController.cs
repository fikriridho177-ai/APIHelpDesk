using AuthUtility.Api.Sensus;
using AuthUtility.Application.DTOs.Sensus;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

// Master sensus: jenis sensus & parameter (SNS_MASTER_JENIS).
// Lokasi (kebun / blok) = persil dari MyPAD & RSUPPKB, lihat SensusPersilController / PersilReader.
// GET boleh untuk semua user yang punya akses menu sensus (dipakai sebagai filter di Approval/Monitoring/Analisis).
// Hapus = nonaktifkan (Active = false) agar data sensus lama tetap utuh.
[ApiController]
[Route("api/sensus/master")]
[Authorize]
public class SensusMasterController(AppDbContext db, IPermissionService permissions) : ControllerBase
{
    /* =========================================================
       JENIS SENSUS
    ========================================================= */

    [HttpGet("jenis")]
    public async Task<IActionResult> GetJenis()
    {
        if (!await SensusHelper.HasAnyAccessAsync(db, permissions, User)) return Forbid();
        return Ok(await QueryJenis(db, activeOnly: false));
    }

    [HttpPost("jenis")]
    public async Task<IActionResult> CreateJenis(SaveSnsJenisRequest body)
    {
        if (!await Can(SnsMenu.MasterJenis, MenuPermission.Create)) return Forbid();
        var error = await ValidateJenis(body, null);
        if (error != null) return BadRequest(error);

        var entity = new SnsJenis();
        ApplyJenis(entity, body);
        db.SnsJenis.Add(entity);
        await db.SaveChangesAsync();
        return Ok(entity.Id);
    }

    [HttpPut("jenis/{id:int}")]
    public async Task<IActionResult> UpdateJenis(int id, SaveSnsJenisRequest body)
    {
        if (!await Can(SnsMenu.MasterJenis, MenuPermission.Update)) return Forbid();

        var entity = await db.SnsJenis.FindAsync(id);
        if (entity == null) return NotFound();

        var error = await ValidateJenis(body, id);
        if (error != null) return BadRequest(error);

        ApplyJenis(entity, body);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("jenis/{id:int}")]
    public async Task<IActionResult> DeleteJenis(int id)
    {
        if (!await Can(SnsMenu.MasterJenis, MenuPermission.Delete)) return Forbid();

        var entity = await db.SnsJenis.FindAsync(id);
        if (entity == null) return NotFound();

        entity.Active = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok();
    }

    /* =========================================================
       PARAMETER
    ========================================================= */

    [HttpPost("jenis/{jenisId:int}/parameter")]
    public async Task<IActionResult> CreateParameter(int jenisId, SaveSnsParameterRequest body)
    {
        if (!await Can(SnsMenu.MasterJenis, MenuPermission.Update)) return Forbid();
        if (!await db.SnsJenis.AnyAsync(x => x.Id == jenisId)) return NotFound();

        var error = await ValidateParameter(jenisId, body, null);
        if (error != null) return BadRequest(error);

        var entity = new SnsParameter { JenisId = jenisId };
        ApplyParameter(entity, body);
        db.SnsParameters.Add(entity);
        await db.SaveChangesAsync();
        return Ok(entity.Id);
    }

    [HttpPut("parameter/{id:int}")]
    public async Task<IActionResult> UpdateParameter(int id, SaveSnsParameterRequest body)
    {
        if (!await Can(SnsMenu.MasterJenis, MenuPermission.Update)) return Forbid();

        var entity = await db.SnsParameters.FindAsync(id);
        if (entity == null) return NotFound();

        var error = await ValidateParameter(entity.JenisId, body, id);
        if (error != null) return BadRequest(error);

        ApplyParameter(entity, body);
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("parameter/{id:int}")]
    public async Task<IActionResult> DeleteParameter(int id)
    {
        if (!await Can(SnsMenu.MasterJenis, MenuPermission.Update)) return Forbid();

        var entity = await db.SnsParameters.FindAsync(id);
        if (entity == null) return NotFound();

        // Parameter yang sudah dipakai sensus hanya dinonaktifkan.
        if (await db.SnsDetails.AnyAsync(x => x.ParameterId == id)) entity.Active = false;
        else db.SnsParameters.Remove(entity);

        await db.SaveChangesAsync();
        return Ok();
    }

    /* =========================================================
       QUERY (dipakai juga oleh endpoint Android)
    ========================================================= */

    public static async Task<List<SnsJenisDto>> QueryJenis(AppDbContext db, bool activeOnly)
    {
        var list = await db.SnsJenis.AsNoTracking()
            .Include(x => x.Parameters)
            .Where(x => !activeOnly || x.Active)
            .OrderByDescending(x => x.Id)
            .ToListAsync();

        return list.Select(x => new SnsJenisDto(
                x.Id, x.Code, x.Name, x.Kategori, x.Komoditas, x.MenuCode, x.Description, x.Active,
                x.Parameters
                    .Where(p => !activeOnly || p.Active)
                    .OrderByDescending(p => p.Id)
                    .Select(p => new SnsParameterDto(
                        p.Id, p.JenisId, p.Code, p.Name, p.Satuan, p.TipeData, p.Opsi, p.Wajib, p.IsIndikator,
                        p.AmbangSedang, p.AmbangBerat, p.Active))
                    .ToList()))
            .ToList();
    }

    /* =========================================================
       HELPER
    ========================================================= */

    private Task<bool> Can(string menuCode, MenuPermission permission) =>
        permissions.HasPermissionAsync(User, menuCode, permission);

    private async Task<object?> ValidateJenis(SaveSnsJenisRequest body, int? id)
    {
        if (string.IsNullOrWhiteSpace(body.Code) || body.Code.Trim().Length > 30 ||
            string.IsNullOrWhiteSpace(body.Name) || body.Name.Trim().Length > 150)
            return new { message = "Kode (maks. 30) dan nama (maks. 150) wajib diisi." };
        if (body.Kategori?.Trim().ToUpperInvariant() is not ("HPT" or "TANAMAN"))
            return new { message = "Kategori harus HPT atau TANAMAN." };
        if (!SnsKomoditas.Jenis.Contains(body.Komoditas?.Trim().ToUpperInvariant()))
            return new { message = "Komoditas harus KELAPA, NANAS, atau SEMUA." };

        var code = body.Code.Trim().ToUpperInvariant();
        if (await db.SnsJenis.AnyAsync(x => x.Code == code && x.Id != id))
            return new { message = "Kode jenis sensus sudah dipakai." };

        return null;
    }

    private static void ApplyJenis(SnsJenis entity, SaveSnsJenisRequest body)
    {
        entity.Code = body.Code.Trim().ToUpperInvariant();
        entity.Name = body.Name.Trim();
        entity.Kategori = body.Kategori.Trim().ToUpperInvariant();
        entity.Komoditas = body.Komoditas.Trim().ToUpperInvariant();
        entity.MenuCode = string.IsNullOrWhiteSpace(body.MenuCode) ? null : body.MenuCode.Trim();
        entity.Description = body.Description?.Trim();
        entity.Active = body.Active;
    }

    private async Task<object?> ValidateParameter(int jenisId, SaveSnsParameterRequest body, int? id)
    {
        if (string.IsNullOrWhiteSpace(body.Code) || body.Code.Trim().Length > 30 ||
            string.IsNullOrWhiteSpace(body.Name) || body.Name.Trim().Length > 150)
            return new { message = "Kode (maks. 30) dan nama (maks. 150) wajib diisi." };

        var tipe = body.TipeData?.Trim().ToUpperInvariant();
        if (!SnsTipeData.All.Contains(tipe))
            return new { message = "Tipe data harus NUMBER, TEXT, BOOLEAN, atau OPTION." };
        if (tipe == "OPTION" && string.IsNullOrWhiteSpace(body.Opsi))
            return new { message = "Isi pilihan (dipisah koma) untuk tipe OPTION." };
        if (body.IsIndikator && tipe != "NUMBER")
            return new { message = "Parameter indikator harus bertipe NUMBER." };
        if (body.AmbangSedang != null && body.AmbangBerat != null && body.AmbangSedang > body.AmbangBerat)
            return new { message = "Ambang sedang tidak boleh lebih besar dari ambang berat." };

        var code = body.Code.Trim().ToUpperInvariant();
        if (await db.SnsParameters.AnyAsync(x => x.JenisId == jenisId && x.Code == code && x.Id != id))
            return new { message = "Kode parameter sudah dipakai di jenis sensus ini." };

        return null;
    }

    private static void ApplyParameter(SnsParameter entity, SaveSnsParameterRequest body)
    {
        entity.Code = body.Code.Trim().ToUpperInvariant();
        entity.Name = body.Name.Trim();
        entity.Satuan = string.IsNullOrWhiteSpace(body.Satuan) ? null : body.Satuan.Trim();
        entity.TipeData = body.TipeData.Trim().ToUpperInvariant();
        entity.Opsi = entity.TipeData == "OPTION" ? body.Opsi?.Trim() : null;
        entity.Wajib = body.Wajib;
        entity.IsIndikator = body.IsIndikator;
        entity.AmbangSedang = body.AmbangSedang;
        entity.AmbangBerat = body.AmbangBerat;
        entity.Active = body.Active;
    }
}
