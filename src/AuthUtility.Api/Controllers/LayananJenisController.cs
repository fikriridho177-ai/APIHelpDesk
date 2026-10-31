using System.Security.Claims;
using AuthUtility.Api.Layanan;
using AuthUtility.Application.DTOs.Layanan;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

// Master Jenis Permintaan (pilihan "Jenis Permintaan" di form pengajuan), pembeda per divisi.
//   GET  ?divisionCode=LYN_GIS          -> jenis aktif untuk form (divisi yang boleh dilihat user)
//   GET  ?all=true                      -> semua jenis termasuk nonaktif (halaman Master, hak Read PMT_MASTER_JENIS)
//   POST / PUT / DELETE                 -> hak Create / Update / Delete di PMT_MASTER_JENIS
//                                          + petugas divisi tsb (Update / Approve di menu divisi)
[ApiController]
[Route("api/layanan/jenis")]
[Authorize]
public class LayananJenisController(AppDbContext db) : ControllerBase
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetList(string? divisionCode, bool all = false)
    {
        var access = await LayananAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();
        if (all && !access.Has(LayananAccess.MasterJenisCode, MenuPermission.Read)) return NoAccess("membuka master jenis permintaan");

        var divisions = access.Divisions
            .Where(x => access.Has(x.Id, MenuPermission.View))
            .Where(x => string.IsNullOrWhiteSpace(divisionCode) || x.Code == divisionCode.Trim())
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToList();
        var divisionIds = divisions.Select(x => x.Id).ToList();

        var items = await db.LynJenisPermintaan
            .AsNoTracking()
            .Where(x => divisionIds.Contains(x.DivisionMenuId) && (all || x.Active))
            .ToListAsync();

        var usedCounts = all
            ? await db.LynRequests
                .AsNoTracking()
                .Where(x => x.JenisId != null)
                .GroupBy(x => x.JenisId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count)
            : new Dictionary<int, int>();

        var order = divisions.Select((d, i) => (d.Id, i)).ToDictionary(x => x.Id, x => x.i);

        var result = items
            .OrderBy(x => order[x.DivisionMenuId])
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => ToDto(x, access.Menus[x.DivisionMenuId], usedCounts.GetValueOrDefault(x.Id)))
            .ToList();

        return Ok(new LynJenisListDto(
            divisions.Select(x => new LynDivisionOptionDto(x.Id, x.Code, x.Name, access.CanManageDivision(x.Id))).ToList(),
            result));
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveLynJenisRequest body)
    {
        var access = await LayananAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();
        if (!access.Has(LayananAccess.MasterJenisCode, MenuPermission.Create)) return NoAccess("menambah jenis permintaan");

        var entity = new LynJenisPermintaan();
        var error = await Apply(entity, body, access);
        if (error != null) return error;

        db.LynJenisPermintaan.Add(entity);
        await db.SaveChangesAsync();

        return Ok(ToDto(entity, access.Menus[entity.DivisionMenuId], 0));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveLynJenisRequest body)
    {
        var access = await LayananAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();
        if (!access.Has(LayananAccess.MasterJenisCode, MenuPermission.Update)) return NoAccess("mengubah jenis permintaan");

        var entity = await db.LynJenisPermintaan.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound(new { message = "Jenis permintaan tidak ditemukan." });
        if (!access.CanManageDivision(entity.DivisionMenuId)) return NoAccess("mengubah jenis permintaan divisi ini");

        var error = await Apply(entity, body, access);
        if (error != null) return error;

        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var used = await db.LynRequests.CountAsync(x => x.JenisId == id);
        return Ok(ToDto(entity, access.Menus[entity.DivisionMenuId], used));
    }

    // Jenis yang sudah dipakai tiket hanya dinonaktifkan supaya riwayat tiket tetap utuh.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var access = await LayananAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();
        if (!access.Has(LayananAccess.MasterJenisCode, MenuPermission.Delete)) return NoAccess("menghapus jenis permintaan");

        var entity = await db.LynJenisPermintaan.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound(new { message = "Jenis permintaan tidak ditemukan." });
        if (!access.CanManageDivision(entity.DivisionMenuId)) return NoAccess("menghapus jenis permintaan divisi ini");

        if (await db.LynRequests.AnyAsync(x => x.JenisId == id))
        {
            entity.Active = false;
            entity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Ok(new { deactivated = true, message = "Jenis sudah dipakai tiket, jadi hanya dinonaktifkan." });
        }

        db.LynJenisPermintaan.Remove(entity);
        await db.SaveChangesAsync();
        return Ok(new { deactivated = false, message = "Jenis permintaan dihapus." });
    }

    /* ---------- Helper ---------- */

    private async Task<IActionResult?> Apply(LynJenisPermintaan entity, SaveLynJenisRequest body, LayananAccess access)
    {
        if (!access.Menus.TryGetValue(body.DivisionMenuId, out var division) || !access.IsDivision(division))
            return BadRequest(new { message = "Divisi tidak ditemukan atau tidak aktif." });
        if (!access.CanManageDivision(division.Id))
            return NoAccess($"mengatur jenis permintaan divisi {division.Name}");

        var name = (body.Name ?? "").Trim();
        if (name.Length == 0) return BadRequest(new { message = "Nama jenis wajib diisi." });
        if (name.Length > 150) return BadRequest(new { message = "Nama jenis maksimal 150 karakter." });

        var description = body.Description?.Trim();
        if (description?.Length > 500) return BadRequest(new { message = "Keterangan maksimal 500 karakter." });

        // Kode kosong -> dibuat dari kode divisi + nama, mis. LYN_GIS + "Pembuatan Peta" -> LYN_GIS_PEMBUATAN_PETA.
        var code = LynRequestRules.NormalizeCode(string.IsNullOrWhiteSpace(body.Code) ? $"{division.Code}_{name}" : body.Code);
        if (code.Length == 0) return BadRequest(new { message = "Kode jenis wajib diisi (huruf/angka)." });
        if (await db.LynJenisPermintaan.AnyAsync(x => x.Code == code && x.Id != entity.Id))
            return BadRequest(new { message = $"Kode {code} sudah dipakai jenis lain." });

        entity.DivisionMenuId = division.Id;
        entity.Code = code;
        entity.Name = name;
        entity.Description = string.IsNullOrEmpty(description) ? null : description;
        entity.IsOther = body.IsOther;
        entity.SortOrder = body.SortOrder;
        entity.Active = body.Active;
        return null;
    }

    private static LynJenisDto ToDto(LynJenisPermintaan x, UtlMenu division, int used) =>
        new(x.Id, x.DivisionMenuId, division.Code, division.Name, x.Code, x.Name, x.Description, x.IsOther, x.SortOrder, x.Active, used);

    private ObjectResult NoAccess(string action) =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = $"Anda tidak memiliki akses untuk {action}." });
}
