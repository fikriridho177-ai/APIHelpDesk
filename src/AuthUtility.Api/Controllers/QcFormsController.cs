using System.Security.Claims;
using AuthUtility.Api.Qc;
using AuthUtility.Application.DTOs.Qc;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

// Master jenis pemeriksaan QC. Read-only list/detail untuk dipakai form "Buat Inspeksi".
// CRUD form (tambah/ubah/aktif-nonaktif) lewat admin SQL / halaman master tersendiri nanti.
[ApiController]
[Route("api/qc/forms")]
[Authorize]
public class QcFormsController(AppDbContext db) : ControllerBase
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Daftar form aktif. ?commodity=KELAPA|NANAS
    [HttpGet]
    public async Task<IActionResult> GetForms(string? commodity, CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var q = db.QcForms.AsNoTracking().Where(x => x.Active);
        if (!string.IsNullOrWhiteSpace(commodity))
            q = q.Where(x => x.Commodity == commodity.ToUpperInvariant());

        var forms = await q.OrderBy(x => x.Name).ToListAsync(ct);

        return Ok(forms.Select(x => new QcFormDto(
            x.Id, x.Code, x.Name, x.Commodity, x.Description, x.Active)));
    }

    // Detail satu form.
    [HttpGet("{code}")]
    public async Task<IActionResult> GetForm(string code, CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var form = await db.QcForms.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, ct);
        if (form == null) return NotFound(new { message = "Form QC tidak ditemukan." });

        return Ok(new QcFormDto(form.Id, form.Code, form.Name, form.Commodity, form.Description, form.Active));
    }
}
