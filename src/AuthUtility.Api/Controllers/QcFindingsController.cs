using System.Security.Claims;
using AuthUtility.Api.Qc;
using AuthUtility.Application.DTOs.Qc;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

// Finding / temuan QC — one-to-many per inspeksi. Lifecycle terpisah dari inspeksi.
// Phase 1: OPEN -> FOLLOW_UP -> VERIFIED -> CLOSED. (WAITING_VERIFICATION didefinisikan
// untuk flow penuh, belum digerakkan endpoint.)
[ApiController]
[Route("api/qc/findings")]
[Authorize]
public class QcFindingsController(
    AppDbContext db,
    QcNumberGenerator numbers,
    QcStatusEngine engine
) : ControllerBase
{
    private const int MaxListSize = 500;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /* =========================================================
       LIST
    ========================================================= */

    // GET /api/qc/findings  ?status&severity&overdue&inspectionId&search
    [HttpGet]
    public async Task<IActionResult> GetFindings(
        string? status, string? severity, bool? overdue, long? inspectionId,
        string? search, CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var q = db.QcFindings.AsNoTracking().Include(f => f.Inspection).AsQueryable();

        // Visibility: handler lihat semua; bukan handler hanya temuan di inspeksi milik sendiri.
        if (!access.IsHandler)
            q = q.Where(f => f.Inspection.CreatedBy == access.User.UserId);

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(f => f.Status == status.Trim());
        if (!string.IsNullOrWhiteSpace(severity))
            q = q.Where(f => f.Severity == severity.Trim());
        if (inspectionId != null)
            q = q.Where(f => f.InspectionId == inspectionId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim().ToLower();
            q = q.Where(f =>
                f.Description.ToLower().Contains(kw) ||
                (f.Category != null && f.Category.ToLower().Contains(kw)) ||
                (f.FindingNo != null && f.FindingNo.ToLower().Contains(kw)));
        }

        var rows = await q.OrderByDescending(f => f.Id).Take(MaxListSize).ToListAsync(ct);

        var items = rows
            .Where(f => overdue != true || (f.Status != QcFindingStatus.Closed && f.TargetDate < today))
            .Select(f => BuildListItem(f, f.Inspection?.InspectionNo, today))
            .ToList();

        return Ok(items);
    }

    // GET /api/qc/findings/{id}
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetFinding(long id, CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var f = await db.QcFindings.AsNoTracking()
            .Include(f => f.Inspection)
            .Include(f => f.VerifiedBy)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f == null || !access.CanSee(f.Inspection!))
            return NotFound(new { message = "Temuan tidak ditemukan." });

        return Ok(BuildDetail(f, today: DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))));
    }

    /* =========================================================
       CREATE
    ========================================================= */

    // POST /api/qc/findings
    [HttpPost]
    public async Task<IActionResult> CreateFinding([FromBody] CreateQcFindingRequest body, CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();
        if (!access.Has(QcAccess.MenuFinding, MenuPermission.Create) && !access.IsSuperAdmin)
            return NoAccess("membuat temuan QC.");

        var inspection = await db.QcInspections.AsNoTracking()
            .Include(x => x.Form)
            .FirstOrDefaultAsync(x => x.Id == body.InspectionId, ct);
        if (inspection == null || !access.CanSee(inspection))
            return NotFound(new { message = "Inspeksi tidak ditemukan." });

        if (!engine.CanCreateFinding(inspection.Status))
            return BadRequest(new { message = "Temuan tidak bisa dibuat pada status inspeksi ini." });

        if (string.IsNullOrWhiteSpace(body.Description))
            return BadRequest(new { message = "Deskripsi temuan wajib diisi." });
        if (!QcFindingSeverity.All.Contains(body.Severity))
            return BadRequest(new { message = "Severity tidak valid (MINOR/MAJOR/CRITICAL)." });

        var finding = new QcFinding
        {
            InspectionId = inspection.Id,
            Category = Trim(body.Category),
            Description = body.Description.Trim(),
            Severity = body.Severity,
            PicUserId = body.PicUserId,
            PicNameSnapshot = Trim(body.PicName),
            TargetDate = body.TargetDate,
            Status = QcFindingStatus.Open,
            CreatedBy = access.User.UserId
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.QcFindings.Add(finding);
        await db.SaveChangesAsync(ct);

        // Nomor temuan dari komoditas inspeksi.
        finding.FindingNo = numbers.GenerateFindingNo(inspection.Commodity, finding.Id);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetFinding(finding.Id, ct);
    }

    /* =========================================================
       FOLLOW-UP / VERIFY / CLOSE
    ========================================================= */

    // POST /api/qc/findings/{id}/follow-up  (OPEN|FOLLOW_UP -> FOLLOW_UP, tambah catatan)
    [HttpPost("{id:long}/follow-up")]
    public async Task<IActionResult> FollowUp(long id, [FromBody] QcFindingFollowUpRequest? body, CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var f = await db.QcFindings.Include(f => f.Inspection).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f == null || !access.CanSee(f.Inspection!))
            return NotFound(new { message = "Temuan tidak ditemukan." });

        if (!access.Has(QcAccess.MenuFinding, MenuPermission.Update) && !access.IsSuperAdmin)
            return NoAccess("menambah tindak lanjut temuan.");

        if (f.Status != QcFindingStatus.Open && f.Status != QcFindingStatus.FollowUp)
            return BadRequest(new { message = "Tindak lanjut hanya bisa dilakukan pada temuan OPEN / FOLLOW_UP." });

        f.Status = QcFindingStatus.FollowUp;
        f.FollowUpNote = AppendNote(f.FollowUpNote, body?.FollowUpNote, access.User.Name);
        f.UpdatedBy = access.User.UserId;
        f.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetFinding(id, ct);
    }

    // POST /api/qc/findings/{id}/verify  (FOLLOW_UP|WAITING_VERIFICATION -> VERIFIED)
    [HttpPost("{id:long}/verify")]
    public async Task<IActionResult> Verify(long id, CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var f = await db.QcFindings.Include(f => f.Inspection).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f == null || !access.CanSee(f.Inspection!))
            return NotFound(new { message = "Temuan tidak ditemukan." });

        if (!access.Has(QcAccess.MenuFinding, MenuPermission.Update) && !access.IsSuperAdmin)
            return NoAccess("memverifikasi temuan.");

        if (f.Status != QcFindingStatus.FollowUp && f.Status != QcFindingStatus.WaitingVerification)
            return BadRequest(new { message = "Verifikasi hanya bisa dilakukan pada temuan FOLLOW_UP." });

        f.Status = QcFindingStatus.Verified;
        f.VerifiedByUserId = access.UserId;
        f.VerifiedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        f.UpdatedBy = access.User.UserId;
        f.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetFinding(id, ct);
    }

    // POST /api/qc/findings/{id}/close  (VERIFIED -> CLOSED)
    [HttpPost("{id:long}/close")]
    public async Task<IActionResult> CloseFinding(long id, CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var f = await db.QcFindings.Include(f => f.Inspection).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f == null || !access.CanSee(f.Inspection!))
            return NotFound(new { message = "Temuan tidak ditemukan." });

        if (!access.Has(QcAccess.MenuFinding, MenuPermission.Update) && !access.IsSuperAdmin)
            return NoAccess("menutup temuan.");

        if (f.Status != QcFindingStatus.Verified)
            return BadRequest(new { message = "Temuan hanya bisa ditutup setelah VERIFIED." });

        f.Status = QcFindingStatus.Closed;
        f.UpdatedBy = access.User.UserId;
        f.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetFinding(id, ct);
    }

    /* =========================================================
       HELPERS
    ========================================================= */

    private static QcFindingListItemDto BuildListItem(QcFinding f, string? inspectionNo, DateOnly today) =>
        new(f.Id, f.FindingNo, f.InspectionId, inspectionNo, f.Category, f.Description, f.Severity,
            f.Status, f.PicUserId, f.PicNameSnapshot, f.TargetDate,
            f.Status != QcFindingStatus.Closed && f.TargetDate.HasValue && f.TargetDate.Value < today,
            f.CreatedAt);

    private static QcFindingDetailDto BuildDetail(QcFinding f, DateOnly today) =>
        new(f.Id, f.FindingNo, f.InspectionId, f.Inspection?.InspectionNo, f.Inspection?.Status ?? "",
            f.Category, f.Description, f.Severity, f.Status, f.PicUserId, f.PicNameSnapshot,
            f.TargetDate, f.FollowUpNote, f.VerifiedByUserId, f.VerifiedBy?.Name, f.VerifiedDate,
            f.Status != QcFindingStatus.Closed && f.TargetDate.HasValue && f.TargetDate.Value < today,
            f.CreatedBy, f.CreatedAt, f.UpdatedAt);

    private static string? AppendNote(string? existing, string? add, string userName)
    {
        if (string.IsNullOrWhiteSpace(add)) return existing;
        var stamp = DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm");
        var line = $"[{stamp} {userName}] {add.Trim()}";
        return string.IsNullOrWhiteSpace(existing) ? line : $"{existing}\n{line}";
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private ObjectResult NoAccess(string action) =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = $"Anda tidak memiliki akses untuk {action}." });
}
