using System.Security.Claims;
using System.Text.Json;
using AuthUtility.Api.Qc;
using AuthUtility.Api.Sensus;
using AuthUtility.Application.DTOs.Qc;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

// Inti modul QC: satu tabel transaksi QcInspection untuk semua form.
// Approval & transisi status 100% di backend; frontend hanya mengirim aksi.
// Setiap transisi penting menulis QcHistory di transaksi yang sama.
[ApiController]
[Route("api/qc/inspections")]
[Authorize]
public class QcInspectionsController(
    AppDbContext db,
    QcStatusEngine engine,
    QcNumberGenerator numbers,
    QcApprovalFlowConfig flow
) : ControllerBase
{
    private const int MaxListSize = 500;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // QC master data uses the same access as creating inspections, without Sensus permissions.
    [HttpGet("masters/persil")]
    public async Task<IActionResult> GetPersils(string commodity,
        [FromServices] PersilReader reader, [FromServices] ILogger<QcInspectionsController> logger)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();
        if (!access.Has(QcAccess.MenuBuatInspeksi, MenuPermission.Create) && !access.IsSuperAdmin)
            return NoAccess("membuat inspeksi QC.");

        PersilReader.Source source;
        try { source = PersilReader.QcSource(commodity); }
        catch (ArgumentException) { return BadRequest(new { message = "Komoditas QC harus NANAS atau KELAPA." }); }

        try
        {
            var rows = await reader.ListQcAsync(source);
            var terpotong = rows.Count > PersilReader.MaxRows;
            if (terpotong) rows.RemoveAt(rows.Count - 1);
            return Ok(new { sumber = source.Key, total = rows.Count, terpotong,
                rows = rows.OrderBy(x => x.PersilKode).ThenBy(x => x.Subpersil).ToList() });
        }
        catch (System.Data.Common.DbException error)
        {
            return SensusPersilController.SourceError(logger, source, error);
        }
        catch (InvalidOperationException error)
        {
            logger.LogError(error, "Kolom view QC MyPAD tidak sesuai: {View}", source.Table);
            return StatusCode(503, new { message = "Struktur view persil MyPAD belum sesuai. Hubungi administrator." });
        }
    }

    /* =========================================================
       LIST
    ========================================================= */

    // GET /api/qc/inspections
    [HttpGet]
    public async Task<IActionResult> GetInspections(
        string? formCode, string? status, int? companyId, string? wilayahKode,
        string? commodity, int? periodYear, int? periodMonth,
        string? search, bool mine = false, CancellationToken ct = default)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var q = db.QcInspections.AsNoTracking().Include(x => x.Form).AsQueryable();

        // Visibility: milik sendiri, atau handler (Read/Approve). mine=true = milik sendiri saja.
        if (mine || !access.IsHandler)
            q = q.Where(x => x.CreatedBy == access.User.UserId);

        if (!string.IsNullOrWhiteSpace(formCode))
            q = q.Where(x => x.Form.Code == formCode.Trim());
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(x => x.Status == status.Trim());
        if (companyId != null)
            q = q.Where(x => x.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(wilayahKode))
            q = q.Where(x => x.WilayahKode == wilayahKode.Trim());
        if (!string.IsNullOrWhiteSpace(commodity))
            q = q.Where(x => x.Commodity == commodity.Trim().ToUpperInvariant());
        if (periodYear != null)
            q = q.Where(x => x.PeriodYear == periodYear);
        if (periodMonth != null)
            q = q.Where(x => x.PeriodMonth == periodMonth);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim().ToLower();
            q = q.Where(x =>
                (x.InspectionNo != null && x.InspectionNo.ToLower().Contains(kw)) ||
                (x.CompanyNameSnapshot != null && x.CompanyNameSnapshot.ToLower().Contains(kw)) ||
                (x.PersilKode != null && x.PersilKode.ToLower().Contains(kw)) ||
                (x.WilayahKode != null && x.WilayahKode.ToLower().Contains(kw)));
        }

        var rows = await q.OrderByDescending(x => x.Id).Take(MaxListSize).ToListAsync(ct);

        // Finding counts dalam satu query (hindari N+1).
        var ids = rows.Select(x => x.Id).ToList();
        var findingCounts = await db.QcFindings.AsNoTracking()
            .Where(x => ids.Contains(x.InspectionId))
            .Select(x => new { x.InspectionId, x.Status, x.TargetDate })
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var byInspection = findingCounts
            .GroupBy(x => x.InspectionId)
            .ToDictionary(g => g.Key, g => (
                Active: g.Count(f => f.Status != QcFindingStatus.Closed),
                Overdue: g.Count(f => f.Status != QcFindingStatus.Closed && f.TargetDate.HasValue && f.TargetDate.Value < today)));

        var items = rows.Select(x =>
        {
            byInspection.TryGetValue(x.Id, out var c);
            return BuildListItem(x, access, c.Active, c.Overdue);
        }).ToList();

        return Ok(new QcInspectionListResult(items.Count, items));
    }

    /* =========================================================
       DETAIL
    ========================================================= */

    // GET /api/qc/inspections/{id}
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetInspection(long id, CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var x = await db.QcInspections.AsNoTracking()
            .Include(x => x.Form)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (x == null || !access.CanSee(x)) return NotFound(new { message = "Inspeksi tidak ditemukan." });

        var approvals = await db.QcApprovals.AsNoTracking()
            .Where(a => a.InspectionId == id).OrderBy(a => a.Level).ToListAsync(ct);
        var findings = await db.QcFindings.AsNoTracking()
            .Where(f => f.InspectionId == id).OrderBy(f => f.Id).ToListAsync(ct);
        var histories = await db.QcHistories.AsNoTracking()
            .Where(h => h.InspectionId == id).OrderBy(h => h.Id).ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var activeCount = findings.Count(f => f.Status != QcFindingStatus.Closed);
        var overdueCount = findings.Count(f => f.Status != QcFindingStatus.Closed && f.TargetDate < today);

        var summary = BuildListItem(x, access, activeCount, overdueCount);

        var detail = new QcInspectionDetailDto(
            summary,
            ParseData(x.DataJson),
            x.KodeAfdeling,
            x.PersilId,
            approvals.Select(a => new QcApprovalDto(
                a.Id, a.Level, a.ApproverUserId, a.ApproverNameSnapshot, a.ApproverGroupId,
                a.Action, a.Status, a.Comment, a.ApprovedAt, a.CreatedAt, a.UpdatedAt)).ToList(),
            findings.Select(f => BuildFindingListItem(f, x.InspectionNo, today)).ToList(),
            histories.Select(h => new QcHistoryDto(
                h.Id, h.Action, h.FromStatus, h.ToStatus, h.ApprovalLevel, h.Note,
                h.UserId, h.User?.Name, h.CreatedAt)).ToList());

        return Ok(detail);
    }

    /* =========================================================
       CREATE / UPDATE
    ========================================================= */

    // POST /api/qc/inspections  (buat draft)
    [HttpPost]
    public async Task<IActionResult> CreateInspection([FromBody] CreateQcInspectionRequest body, CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();
        if (!access.Has(QcAccess.MenuBuatInspeksi, MenuPermission.Create) && !access.IsSuperAdmin)
            return NoAccess("membuat inspeksi QC.");

        if (string.IsNullOrWhiteSpace(body.FormCode))
            return BadRequest(new { message = "FormCode wajib diisi." });

        var form = await db.QcForms.AsNoTracking().FirstOrDefaultAsync(x => x.Code == body.FormCode.Trim(), ct);
        if (form == null || !form.Active)
            return BadRequest(new { message = "Form QC tidak ditemukan atau tidak aktif." });

        var date = body.InspectionDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (body.PeriodMonth is < 1 or > 12)
            return BadRequest(new { message = "PeriodMonth harus 1-12." });

        var inspection = new QcInspection
        {
            FormId = form.Id,
            InspectionDate = date,
            CompanyId = body.CompanyId,
            CompanyNameSnapshot = Trim(body.CompanyName),
            WilayahKode = Trim(body.WilayahKode),
            PersilSumber = Trim(body.PersilSumber),
            PersilId = Trim(body.PersilId),
            PersilKode = Trim(body.PersilKode),
            KodeAfdeling = Trim(body.KodeAfdeling),
            Commodity = form.Commodity,
            PeriodMonth = body.PeriodMonth,
            PeriodYear = body.PeriodYear,
            Status = QcInspectionStatus.Draft,
            CurrentApprovalLevel = 0,
            DataJson = body.Data?.RootElement.GetRawText(),
            CreatedBy = access.User.UserId
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        db.QcInspections.Add(inspection);
        await db.SaveChangesAsync(ct);

        // Nomor di-generate sesudah insert (dari identity Id).
        inspection.InspectionNo = numbers.GenerateInspectionNo(form.Commodity, inspection.Id);

        db.QcHistories.Add(new QcHistory
        {
            InspectionId = inspection.Id,
            UserId = access.UserId,
            Action = QcHistoryAction.Create,
            ToStatus = QcInspectionStatus.Draft,
            Note = $"Draft inspeksi {form.Name} dibuat."
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetInspection(inspection.Id, ct);
    }

    // PUT /api/qc/inspections/{id}  (edit draft / saat revisi)
    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateInspection(long id, [FromBody] UpdateQcInspectionRequest body, CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var x = await db.QcInspections.Include(x => x.Form).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (x == null || !access.CanSee(x)) return NotFound(new { message = "Inspeksi tidak ditemukan." });

        if (!engine.CanEdit(x.Status))
            return BadRequest(new { message = "Inspeksi tidak dapat diubah pada status ini." });
        if (!access.IsOwner(x) && !access.IsSuperAdmin)
            return NoAccess("mengubah inspeksi ini.");

        if (body.PeriodMonth is < 1 or > 12)
            return BadRequest(new { message = "PeriodMonth harus 1-12." });

        if (body.InspectionDate != null) x.InspectionDate = body.InspectionDate.Value;
        if (body.CompanyId != null) x.CompanyId = body.CompanyId;
        if (body.CompanyName != null) x.CompanyNameSnapshot = Trim(body.CompanyName);
        if (body.WilayahKode != null) x.WilayahKode = Trim(body.WilayahKode);
        if (body.PersilSumber != null) x.PersilSumber = Trim(body.PersilSumber);
        if (body.PersilId != null) x.PersilId = Trim(body.PersilId);
        if (body.PersilKode != null) x.PersilKode = Trim(body.PersilKode);
        if (body.KodeAfdeling != null) x.KodeAfdeling = Trim(body.KodeAfdeling);
        if (body.PeriodMonth != null) x.PeriodMonth = body.PeriodMonth;
        if (body.PeriodYear != null) x.PeriodYear = body.PeriodYear;
        if (body.Data != null) x.DataJson = body.Data.RootElement.GetRawText();

        x.UpdatedBy = access.User.UserId;
        x.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.QcHistories.Add(new QcHistory
        {
            InspectionId = x.Id,
            UserId = access.UserId,
            Action = QcHistoryAction.Update,
            FromStatus = x.Status,
            ToStatus = x.Status,
            Note = "Inspeksi diperbarui."
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetInspection(id, ct);
    }

    /* =========================================================
       SUBMIT / RESUBMIT
    ========================================================= */

    // POST /api/qc/inspections/{id}/submit
    [HttpPost("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id, CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var x = await db.QcInspections.Include(x => x.Form).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (x == null || !access.CanSee(x)) return NotFound(new { message = "Inspeksi tidak ditemukan." });

        if (!access.IsOwner(x) && !access.IsSuperAdmin)
            return NoAccess("men-submit inspeksi ini.");
        if (!engine.CanSubmit(x.Status))
            return BadRequest(new { message = "Hanya inspeksi DRAFT yang bisa di-submit." });

        var fromStatus = x.Status;
        x.Status = QcInspectionStatus.WaitingApproval;
        x.CurrentApprovalLevel = 1;
        x.SubmittedAt = DateTime.UtcNow;
        x.UpdatedBy = access.User.UserId;
        x.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Buat baris approval level 1 (PENDING). ApproverGroupId dari config bila ada.
        var level1 = flow.GetLevel(x.Form.Code, 1);
        var approverGroupId = await ResolveGroupIdAsync(level1?.GroupCode, ct);

        db.QcApprovals.Add(new QcApproval
        {
            InspectionId = x.Id,
            Level = 1,
            ApproverGroupId = approverGroupId,
            Action = "",
            Status = QcApprovalStatus.Pending
        });

        db.QcHistories.Add(new QcHistory
        {
            InspectionId = x.Id,
            UserId = access.UserId,
            Action = QcHistoryAction.Submit,
            FromStatus = fromStatus,
            ToStatus = QcInspectionStatus.WaitingApproval,
            ApprovalLevel = 1,
            Note = "Inspeksi di-submit, menunggu approval level 1."
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetInspection(id, ct);
    }

    // POST /api/qc/inspections/{id}/resubmit
    [HttpPost("{id:long}/resubmit")]
    public async Task<IActionResult> Resubmit(long id, [FromBody] QcInspectionActionRequest? body, CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var x = await db.QcInspections.Include(x => x.Form).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (x == null || !access.CanSee(x)) return NotFound(new { message = "Inspeksi tidak ditemukan." });

        if (!access.IsOwner(x) && !access.IsSuperAdmin)
            return NoAccess("men-submit ulang inspeksi ini.");
        if (!engine.CanResubmit(x.Status))
            return BadRequest(new { message = "Hanya inspeksi REVISION_REQUIRED yang bisa di-submit ulang." });

        var fromStatus = x.Status;
        x.Status = QcInspectionStatus.WaitingApproval;
        x.UpdatedBy = access.User.UserId;
        x.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Reset baris approval level aktif -> PENDING.
        var currentRow = await db.QcApprovals
            .FirstOrDefaultAsync(a => a.InspectionId == x.Id && a.Level == x.CurrentApprovalLevel, ct);
        if (currentRow != null)
        {
            currentRow.Status = QcApprovalStatus.Pending;
            currentRow.Action = "";
            currentRow.ApproverUserId = null;
            currentRow.ApproverNameSnapshot = null;
            currentRow.Comment = null;
            currentRow.ApprovedAt = null;
            currentRow.UpdatedAt = DateTime.UtcNow;
        }

        db.QcHistories.Add(new QcHistory
        {
            InspectionId = x.Id,
            UserId = access.UserId,
            Action = QcHistoryAction.Resubmit,
            FromStatus = fromStatus,
            ToStatus = QcInspectionStatus.WaitingApproval,
            ApprovalLevel = x.CurrentApprovalLevel,
            Note = Trim(body?.Comment)
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetInspection(id, ct);
    }

    /* =========================================================
       APPROVAL ACTIONS  (approve / reject / request-revision)
       Satu helper RunApprovalAction: validasi + terapkan transisi.
       Frontend tidak mengirim level — backend pakai CurrentApprovalLevel.
    ========================================================= */

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] QcInspectionActionRequest? body, CancellationToken ct)
        => await RunApprovalAction(id, QcApprovalAction.Approve, body?.Comment, commentRequired: false, "approve", ct);

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, [FromBody] QcInspectionActionRequest? body, CancellationToken ct)
        => await RunApprovalAction(id, QcApprovalAction.Reject, body?.Comment, commentRequired: true, "menolak", ct);

    [HttpPost("{id:long}/request-revision")]
    public async Task<IActionResult> RequestRevision(long id, [FromBody] QcInspectionActionRequest? body, CancellationToken ct)
        => await RunApprovalAction(id, QcApprovalAction.RequestRevision, body?.Comment, commentRequired: true, "meminta revisi pada", ct);

    private async Task<IActionResult> RunApprovalAction(
        long id, string action, string? comment, bool commentRequired, string actionLabel, CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var x = await db.QcInspections.Include(x => x.Form).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (x == null || !access.CanSee(x)) return NotFound(new { message = "Inspeksi tidak ditemukan." });

        if (!engine.CanApprove(x.Status))
            return BadRequest(new { message = "Inspeksi tidak sedang menunggu approval." });

        var level = x.CurrentApprovalLevel;
        var cfgLevel = flow.GetLevel(x.Form.Code, level);
        if (cfgLevel == null)
            return BadRequest(new { message = "Konfigurasi approval level tidak ditemukan." });

        if (!access.CanApproveOn(cfgLevel.MenuCode, cfgLevel.GroupCode))
            return NoAccess($"{actionLabel} level ini.");

        var row = await db.QcApprovals
            .FirstOrDefaultAsync(a => a.InspectionId == x.Id && a.Level == level, ct);
        if (row == null || row.Status != QcApprovalStatus.Pending)
            return BadRequest(new { message = "Tidak ada approval PENDING pada level aktif." });

        if (commentRequired && string.IsNullOrWhiteSpace(comment))
            return BadRequest(new { message = "Catatan wajib diisi untuk aksi ini." });

        var note = Trim(comment);
        var fromStatus = x.Status;
        var now = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Terapkan aksi pada baris approval level aktif.
        row.Action = action;
        row.ApproverUserId = access.UserId;
        row.ApproverNameSnapshot = access.User.Name;
        row.ApproverGroupId = access.User.GroupId;
        row.Comment = note;
        row.ApprovedAt = now;
        row.UpdatedAt = now;

        string toStatus;
        var historyAction =
            action == QcApprovalAction.Approve ? QcHistoryAction.Approve :
            action == QcApprovalAction.Reject ? QcHistoryAction.Reject :
            QcHistoryAction.RequestRevision;

        if (action == QcApprovalAction.Approve)
        {
            row.Status = QcApprovalStatus.Approved;
            var maxLevel = flow.MaxLevel(x.Form.Code);

            if (level < maxLevel)
            {
                // Lanjut ke level berikutnya; status tetap WAITING_APPROVAL.
                x.CurrentApprovalLevel = level + 1;
                x.Status = QcInspectionStatus.WaitingApproval;
                toStatus = QcInspectionStatus.WaitingApproval;

                var nextCfg = flow.GetLevel(x.Form.Code, level + 1);
                var nextApproverGroupId = await ResolveGroupIdAsync(nextCfg?.GroupCode, ct);
                db.QcApprovals.Add(new QcApproval
                {
                    InspectionId = x.Id,
                    Level = level + 1,
                    ApproverGroupId = nextApproverGroupId,
                    Action = "",
                    Status = QcApprovalStatus.Pending
                });
            }
            else
            {
                // Final approval -> APPROVED (ClosedAt tetap NULL).
                x.Status = QcInspectionStatus.Approved;
                x.ApprovedAt = now;
                toStatus = QcInspectionStatus.Approved;
            }
        }
        else if (action == QcApprovalAction.Reject)
        {
            row.Status = QcApprovalStatus.Rejected;
            x.Status = QcInspectionStatus.Rejected;
            x.ClosedAt = now;
            toStatus = QcInspectionStatus.Rejected;
        }
        else // REQUEST_REVISION
        {
            row.Status = QcApprovalStatus.RevisionRequired;
            x.Status = QcInspectionStatus.RevisionRequired;
            toStatus = QcInspectionStatus.RevisionRequired;
        }

        x.UpdatedBy = access.User.UserId;
        x.UpdatedAt = now;

        db.QcHistories.Add(new QcHistory
        {
            InspectionId = x.Id,
            UserId = access.UserId,
            Action = historyAction,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ApprovalLevel = level,
            Note = note
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetInspection(x.Id, ct);
    }

    /* =========================================================
       CLOSE
    ========================================================= */

    // POST /api/qc/inspections/{id}/close  (hanya APPROVED + semua finding CLOSED)
    [HttpPost("{id:long}/close")]
    public async Task<IActionResult> Close(long id, [FromBody] QcInspectionActionRequest? body, CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        if (!access.Has(QcAccess.MenuApproval, MenuPermission.Approve) && !access.IsSuperAdmin)
            return NoAccess("menutup inspeksi.");

        var x = await db.QcInspections.Include(x => x.Form).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (x == null || !access.CanSee(x)) return NotFound(new { message = "Inspeksi tidak ditemukan." });

        var findings = await db.QcFindings.AsNoTracking().Where(f => f.InspectionId == id).ToListAsync(ct);
        if (!engine.CanClose(x, findings))
            return BadRequest(new { message = "Inspeksi hanya bisa ditutup bila status APPROVED dan seluruh temuan sudah CLOSED." });

        var fromStatus = x.Status;
        x.Status = QcInspectionStatus.Closed;
        x.ClosedAt = DateTime.UtcNow;
        x.UpdatedBy = access.User.UserId;
        x.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.QcHistories.Add(new QcHistory
        {
            InspectionId = x.Id,
            UserId = access.UserId,
            Action = QcHistoryAction.Close,
            FromStatus = fromStatus,
            ToStatus = QcInspectionStatus.Closed,
            Note = Trim(body?.Comment)
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetInspection(id, ct);
    }

    /* =========================================================
       HELPERS
    ========================================================= */

    private async Task<int?> ResolveGroupIdAsync(string? groupCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(groupCode)) return null;
        var g = await db.Groups.AsNoTracking().FirstOrDefaultAsync(x => x.Code == groupCode, ct);
        return g?.Id;
    }

    private QcInspectionListItemDto BuildListItem(QcInspection x, QcAccess access, int activeFindings, int overdueFindings)
    {
        var cfgLevel = x.CurrentApprovalLevel > 0 ? flow.GetLevel(x.Form?.Code ?? "", x.CurrentApprovalLevel) : null;
        var approverEligible = cfgLevel != null && access.CanApproveOn(cfgLevel.MenuCode, cfgLevel.GroupCode);

        return new QcInspectionListItemDto(
            x.Id,
            x.InspectionNo,
            x.FormId,
            x.Form?.Code ?? "",
            x.Form?.Name ?? "",
            x.InspectionDate,
            x.CompanyId,
            x.CompanyNameSnapshot,
            x.WilayahKode,
            x.PersilSumber,
            x.PersilKode,
            x.Commodity,
            x.PeriodMonth,
            x.PeriodYear,
            x.Status,
            x.CurrentApprovalLevel,
            x.CreatedAt,
            x.SubmittedAt,
            x.ApprovedAt,
            x.ClosedAt,
            x.CreatedBy,
            null,
            activeFindings,
            overdueFindings,
            AllowedActions(x, access, activeFindings, approverEligible));
    }

    private List<string> AllowedActions(QcInspection x, QcAccess access, int activeFindings, bool approverEligible)
    {
        var a = new List<string>();

        if (access.IsOwner(x) && engine.CanEdit(x.Status))
            a.Add("UPDATE");

        if (access.IsOwner(x) && engine.CanSubmit(x.Status))
            a.Add("SUBMIT");

        if (access.IsOwner(x) && engine.CanResubmit(x.Status))
            a.Add("RESUBMIT");

        if (engine.CanApprove(x.Status) && approverEligible)
        {
            a.Add("APPROVE");
            a.Add("REJECT");
            a.Add("REQUEST_REVISION");
        }

        if (x.Status == QcInspectionStatus.Approved
            && activeFindings == 0
            && (access.Has(QcAccess.MenuApproval, MenuPermission.Approve) || access.IsSuperAdmin))
            a.Add("CLOSE");

        if (engine.CanCreateFinding(x.Status) && access.Has(QcAccess.MenuFinding, MenuPermission.Create))
            a.Add("CREATE_FINDING");

        return a;
    }

    private static QcFindingListItemDto BuildFindingListItem(QcFinding f, string? inspectionNo, DateOnly today) =>
        new(f.Id, f.FindingNo, f.InspectionId, inspectionNo, f.Category, f.Description, f.Severity,
            f.Status, f.PicUserId, f.PicNameSnapshot, f.TargetDate,
            f.Status != QcFindingStatus.Closed && f.TargetDate.HasValue && f.TargetDate.Value < today,
            f.CreatedAt);

    private static JsonDocument? ParseData(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonDocument.Parse(json);

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private ObjectResult NoAccess(string action) =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = $"Anda tidak memiliki akses untuk {action}." });
}
