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
// Alur approval dari master MstQcFormApprovalStep (QcApprovalFlow); setiap
// transisi penting menulis QcHistory di transaksi yang sama.
[ApiController]
[Route("api/qc/inspections")]
[Authorize]
public class QcInspectionsController(
    AppDbContext db,
    QcStatusEngine engine,
    QcNumberGenerator numbers,
    QcApprovalFlow flow
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

    // Referensi header inspeksi: pemborong & mandor aktif (master QC lokal).
    // Sengaja di bawah hak buat inspeksi, bukan QC_MASTER.
    [HttpGet("masters/referensi")]
    public async Task<IActionResult> GetRefs(CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();
        if (!access.Has(QcAccess.MenuBuatInspeksi, MenuPermission.Create) && !access.IsSuperAdmin)
            return NoAccess("membuat inspeksi QC.");

        var pemborong = await db.QcPemborong.AsNoTracking()
            .Where(x => x.Active).OrderBy(x => x.Kode).ToListAsync(ct);
        var mandor = await db.QcMandors.AsNoTracking()
            .Where(x => x.Active).OrderBy(x => x.Name).ToListAsync(ct);
        var pemborongNama = pemborong.ToDictionary(x => x.Id, x => x.Name);

        return Ok(new QcInspectionRefsDto(
            pemborong.Select(x => new QcPemborongRefDto(x.Id, x.Kode, x.Name, x.WilayahKode)).ToList(),
            mandor.Select(x => new QcMandorRefDto(
                x.Id, x.Name, x.WilayahKode, x.KodeAfdeling, x.PemborongId,
                x.PemborongId == null ? null : pemborongNama.GetValueOrDefault(x.PemborongId.Value))).ToList()));
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
                Active: g.Count(f => QcFindingStatus.Active.Contains(f.Status)),
                Overdue: g.Count(f => QcFindingStatus.Active.Contains(f.Status) && f.TargetDate.HasValue && f.TargetDate.Value < today)));

        var stepsMap = await flow.GetStepsForFormsAsync(rows.Select(x => x.FormId), ct);
        var creatorNames = await CreatorNamesAsync(rows.Select(x => x.CreatedBy), ct);

        var items = rows.Select(x =>
        {
            byInspection.TryGetValue(x.Id, out var c);
            var steps = stepsMap.TryGetValue(x.FormId, out var s) ? s : new[] { QcApprovalFlow.FallbackStep };
            return BuildListItem(x, access, c.Active, c.Overdue, steps,
                x.CreatedBy == null ? null : creatorNames.GetValueOrDefault(x.CreatedBy));
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
            .Where(a => a.InspectionId == id)
            .OrderBy(a => a.Revision).ThenBy(a => a.Level).ToListAsync(ct);
        var findings = await db.QcFindings.AsNoTracking()
            .Where(f => f.InspectionId == id).OrderBy(f => f.Id).ToListAsync(ct);
        var histories = await db.QcHistories.AsNoTracking()
            .Where(h => h.InspectionId == id).OrderBy(h => h.Id).ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var activeCount = findings.Count(f => QcFindingStatus.Active.Contains(f.Status));
        var overdueCount = findings.Count(f => QcFindingStatus.Active.Contains(f.Status) && f.TargetDate < today);

        var steps = await flow.GetStepsAsync(x.FormId, ct);
        var creatorNames = await CreatorNamesAsync(new[] { x.CreatedBy }, ct);
        var summary = BuildListItem(x, access, activeCount, overdueCount, steps,
            x.CreatedBy == null ? null : creatorNames.GetValueOrDefault(x.CreatedBy));

        var pemborongNama = x.PemborongId == null ? null
            : await db.QcPemborong.AsNoTracking().Where(p => p.Id == x.PemborongId)
                .Select(p => p.Name).FirstOrDefaultAsync(ct);
        var mandorNama = x.MandorId == null ? null
            : await db.QcMandors.AsNoTracking().Where(m => m.Id == x.MandorId)
                .Select(m => m.Name).FirstOrDefaultAsync(ct);
        var inspectorNama = x.InspectorNameSnapshot;
        if (inspectorNama == null && x.InspectorUserId != null)
            inspectorNama = await db.Users.AsNoTracking().Where(u => u.Id == x.InspectorUserId)
                .Select(u => u.Name).FirstOrDefaultAsync(ct);

        // Panen Nenas: baris typed digabung kembali sebagai data.samples untuk frontend.
        JsonDocument? data;
        if (x.Form?.Code == QcPanenNenasDetail.FormCode)
        {
            var sampel = await db.QcPanenNenasSamples.AsNoTracking()
                .Where(s => s.InspectionId == id).OrderBy(s => s.RowNo).ToListAsync(ct);
            data = QcPanenNenasDetail.WithSamples(x.DataJson, sampel);
        }
        else
        {
            data = ParseData(x.DataJson);
        }

        var detail = new QcInspectionDetailDto(
            summary,
            data,
            x.KodeAfdeling,
            x.PersilId,
            x.PemborongId,
            pemborongNama,
            x.MandorId,
            mandorNama,
            x.InspectorUserId,
            inspectorNama,
            x.NoIcarLama,
            x.Form?.Version ?? 1,
            approvals.Select(a => new QcApprovalDto(
                a.Id, a.Level, a.Revision, a.ApproverUserId, a.ApproverNameSnapshot, a.ApproverGroupId,
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

    // POST /api/qc/inspections  (buat draft; selalu memakai versi form aktif terbaru)
    [HttpPost]
    public async Task<IActionResult> CreateInspection([FromBody] CreateQcInspectionRequest body, CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();
        if (!access.Has(QcAccess.MenuBuatInspeksi, MenuPermission.Create) && !access.IsSuperAdmin)
            return NoAccess("membuat inspeksi QC.");

        if (string.IsNullOrWhiteSpace(body.FormCode))
            return BadRequest(new { message = "FormCode wajib diisi." });

        var form = await db.QcForms.AsNoTracking()
            .Where(x => x.Code == body.FormCode.Trim() && x.Active)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);
        if (form == null)
            return BadRequest(new { message = "Form QC tidak ditemukan atau tidak aktif." });

        var date = body.InspectionDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (body.PeriodMonth is < 1 or > 12)
            return BadRequest(new { message = "PeriodMonth harus 1-12." });

        var refsError = await ValidateRefsAsync(body.PemborongId, body.MandorId, body.InspectorUserId, ct);
        if (refsError != null) return BadRequest(new { message = refsError });

        // Panen Nenas: samples pindah ke tabel typed; DataJson hanya ekstra header.
        List<QcInspSampelPanenNenas>? panenNenasRows = null;
        var dataJson = body.Data?.RootElement.GetRawText();
        if (form.Code == QcPanenNenasDetail.FormCode)
        {
            if (!QcPanenNenasDetail.TryParse(body.Data?.RootElement, out var rows, out var detailError))
                return BadRequest(new { message = detailError });
            panenNenasRows = rows;
            dataJson = QcPanenNenasDetail.StripSamples(dataJson);
        }

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
            PemborongId = body.PemborongId,
            MandorId = body.MandorId,
            InspectorUserId = body.InspectorUserId,
            InspectorNameSnapshot = await ResolveInspectorNameAsync(body.InspectorUserId, body.InspectorName, ct),
            NoIcarLama = Trim(body.NoIcarLama),
            Commodity = form.Commodity,
            PeriodMonth = body.PeriodMonth,
            PeriodYear = body.PeriodYear,
            Status = QcInspectionStatus.Draft,
            CurrentApprovalLevel = 0,
            Revision = 0,
            DataJson = dataJson,
            CreatedBy = access.User.UserId
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        db.QcInspections.Add(inspection);
        await db.SaveChangesAsync(ct);

        if (panenNenasRows != null)
        {
            foreach (var row in panenNenasRows) row.InspectionId = inspection.Id;
            db.QcPanenNenasSamples.AddRange(panenNenasRows);
        }

        // Nomor atomik per scope form|wilayah|tahun|bulan (tanggal inspeksi).
        inspection.InspectionNo = await numbers.GenerateInspectionNoAsync(
            form.Code, inspection.WilayahKode, inspection.InspectionDate, ct);

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

        // FK opsional: null = tidak diubah, 0 = dikosongkan, selain itu divalidasi.
        var pemborongId = body.PemborongId == null ? null : body.PemborongId == 0 ? (int?)null : body.PemborongId;
        var mandorId = body.MandorId == null ? null : body.MandorId == 0 ? (int?)null : body.MandorId;
        var inspectorUserId = body.InspectorUserId == null ? null : body.InspectorUserId == 0 ? (int?)null : body.InspectorUserId;
        var refsError = await ValidateRefsAsync(pemborongId, mandorId, inspectorUserId, ct);
        if (refsError != null) return BadRequest(new { message = refsError });

        // Panen Nenas: validasi dulu sebelum mutasi; samples menggantikan baris typed lama.
        List<QcInspSampelPanenNenas>? panenNenasRows = null;
        if (x.Form?.Code == QcPanenNenasDetail.FormCode && body.Data != null)
        {
            if (!QcPanenNenasDetail.TryParse(body.Data?.RootElement, out var rows, out var detailError))
                return BadRequest(new { message = detailError });
            panenNenasRows = rows;
        }

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
        if (body.PemborongId != null) x.PemborongId = pemborongId;
        if (body.MandorId != null) x.MandorId = mandorId;
        if (body.InspectorUserId != null)
        {
            x.InspectorUserId = inspectorUserId;
            x.InspectorNameSnapshot = await ResolveInspectorNameAsync(inspectorUserId, body.InspectorName, ct);
        }
        else if (body.InspectorName != null) x.InspectorNameSnapshot = Trim(body.InspectorName);
        if (body.NoIcarLama != null) x.NoIcarLama = Trim(body.NoIcarLama);
        if (body.Data != null)
            x.DataJson = panenNenasRows != null
                ? QcPanenNenasDetail.StripSamples(body.Data.RootElement.GetRawText())
                : body.Data.RootElement.GetRawText();

        x.UpdatedBy = access.User.UserId;
        x.UpdatedAt = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (panenNenasRows != null)
        {
            var oldRows = await db.QcPanenNenasSamples.Where(s => s.InspectionId == x.Id).ToListAsync(ct);
            db.QcPanenNenasSamples.RemoveRange(oldRows);
            foreach (var row in panenNenasRows) row.InspectionId = x.Id;
            db.QcPanenNenasSamples.AddRange(panenNenasRows);
        }
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
       Submit: baris pembuat (IsCreatorStep) otomatis APPROVED atas nama
       pembuat, lalu PENDING di step berikutnya. Resubmit: Revision + 1,
       baris approval baru per revisi (baris lama tetap tersimpan).
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

        var steps = await flow.GetStepsAsync(x.FormId, ct);
        var first = steps[0];
        var now = DateTime.UtcNow;
        var fromStatus = x.Status;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        string toStatus;
        if (first.IsCreatorStep)
        {
            db.QcApprovals.Add(CreatorRow(x, first, access, now));
            var next = QcApprovalFlow.NextAfter(steps, first.Level);
            if (next != null)
            {
                db.QcApprovals.Add(PendingRow(x, next));
                x.CurrentApprovalLevel = next.Level;
                toStatus = QcInspectionStatus.Submitted;
            }
            else
            {
                // Hanya langkah pembuat: langsung APPROVED.
                x.CurrentApprovalLevel = first.Level;
                x.ApprovedAt = now;
                toStatus = QcInspectionStatus.Approved;
            }
        }
        else
        {
            db.QcApprovals.Add(PendingRow(x, first));
            x.CurrentApprovalLevel = first.Level;
            toStatus = QcInspectionStatus.Submitted;
        }

        x.Status = toStatus;
        x.SubmittedAt = now;
        x.UpdatedBy = access.User.UserId;
        x.UpdatedAt = now;

        db.QcHistories.Add(new QcHistory
        {
            InspectionId = x.Id,
            UserId = access.UserId,
            Action = QcHistoryAction.Submit,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ApprovalLevel = x.CurrentApprovalLevel,
            Note = toStatus == QcInspectionStatus.Approved
                ? "Inspeksi di-submit; hanya langkah pembuat, langsung disetujui."
                : $"Inspeksi di-submit, menunggu approval level {x.CurrentApprovalLevel}."
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetInspection(id, ct);
    }

    // POST /api/qc/inspections/{id}/resubmit  (setelah REVISION)
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
            return BadRequest(new { message = "Hanya inspeksi REVISION yang bisa di-submit ulang." });

        var steps = await flow.GetStepsAsync(x.FormId, ct);
        var first = steps[0];
        var now = DateTime.UtcNow;
        var fromStatus = x.Status;

        // Level tujuan: level yang meminta revisi; bila master berubah dan level
        // itu tidak ada lagi, mulai dari step manual pertama.
        var target = steps.FirstOrDefault(s => s.Level == x.CurrentApprovalLevel && !s.IsCreatorStep)
            ?? steps.FirstOrDefault(s => !s.IsCreatorStep)
            ?? first;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        x.Revision += 1;
        if (first.IsCreatorStep)
            db.QcApprovals.Add(CreatorRow(x, first, access, now));

        string toStatus;
        if (target.IsCreatorStep)
        {
            x.CurrentApprovalLevel = first.Level;
            x.ApprovedAt = now;
            toStatus = QcInspectionStatus.Approved;
        }
        else
        {
            db.QcApprovals.Add(PendingRow(x, target));
            x.CurrentApprovalLevel = target.Level;
            toStatus = QcInspectionStatus.Submitted;
        }

        x.Status = toStatus;
        x.SubmittedAt = now;
        x.UpdatedBy = access.User.UserId;
        x.UpdatedAt = now;

        var note = Trim(body?.Comment);
        db.QcHistories.Add(new QcHistory
        {
            InspectionId = x.Id,
            UserId = access.UserId,
            Action = QcHistoryAction.Resubmit,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ApprovalLevel = x.CurrentApprovalLevel,
            Note = string.IsNullOrWhiteSpace(note)
                ? $"Revisi ke-{x.Revision} diajukan ulang."
                : $"Revisi ke-{x.Revision} diajukan ulang: {note}"
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetInspection(id, ct);
    }

    /* =========================================================
       APPROVAL ACTIONS  (approve / request-revision)
       Frontend tidak mengirim level — backend pakai CurrentApprovalLevel.
    ========================================================= */

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] QcInspectionActionRequest? body, CancellationToken ct)
        => await RunApprovalAction(id, QcApprovalAction.Approve, body?.Comment, commentRequired: false, "approve", ct);

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
        var steps = await flow.GetStepsAsync(x.FormId, ct);
        var step = steps.FirstOrDefault(s => s.Level == level);
        if (step == null)
            return BadRequest(new { message = "Langkah approval pada level aktif tidak ditemukan di master." });
        if (step.IsCreatorStep)
            return BadRequest(new { message = "Langkah pembuat tidak bisa di-approve manual." });

        if (!access.CanApproveStep(step) || (step.ScopeByAfdeling && !access.MatchesAfdeling(x.KodeAfdeling)))
            return NoAccess($"{actionLabel} level ini.");

        var row = await db.QcApprovals
            .FirstOrDefaultAsync(a => a.InspectionId == x.Id && a.Level == level && a.Revision == x.Revision, ct);
        if (row == null || row.Status != QcApprovalStatus.Pending)
            return BadRequest(new { message = "Tidak ada approval PENDING pada level aktif." });

        if (commentRequired && string.IsNullOrWhiteSpace(comment))
            return BadRequest(new { message = "Catatan wajib diisi untuk aksi ini." });

        var note = Trim(comment);
        var fromStatus = x.Status;
        var now = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        row.Action = action;
        row.ApproverUserId = access.UserId;
        row.ApproverNameSnapshot = access.User.Name;
        row.ApproverGroupId = access.User.GroupId;
        row.Comment = note;
        row.ApprovedAt = now;
        row.UpdatedAt = now;

        string toStatus;
        string historyAction;

        if (action == QcApprovalAction.Approve)
        {
            historyAction = QcHistoryAction.Approve;
            row.Status = QcApprovalStatus.Approved;
            var next = QcApprovalFlow.NextAfter(steps, level);

            if (next != null)
            {
                // Lanjut ke step berikutnya; sudah melewati review pertama -> IN_REVIEW.
                x.CurrentApprovalLevel = next.Level;
                x.Status = QcInspectionStatus.InReview;
                toStatus = QcInspectionStatus.InReview;
                db.QcApprovals.Add(PendingRow(x, next));
            }
            else
            {
                // Final approval -> APPROVED (ClosedAt tetap NULL).
                x.Status = QcInspectionStatus.Approved;
                x.ApprovedAt = now;
                toStatus = QcInspectionStatus.Approved;
            }
        }
        else // REQUEST_REVISION
        {
            historyAction = QcHistoryAction.RequestRevision;
            row.Status = QcApprovalStatus.RevisionRequired;
            x.Status = QcInspectionStatus.Revision;
            toStatus = QcInspectionStatus.Revision;
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

    // POST /api/qc/inspections/{id}/close  (hanya APPROVED + semua finding VERIFIED)
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
            return BadRequest(new { message = "Inspeksi hanya bisa ditutup bila status APPROVED dan seluruh temuan sudah VERIFIED." });

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

    // Baris pembuat: otomatis APPROVED atas nama pembuat saat submit/resubmit.
    private static QcApproval CreatorRow(QcInspection x, QcApprovalFlow.Step step, QcAccess access, DateTime now) =>
        new()
        {
            InspectionId = x.Id,
            Level = step.Level,
            Revision = x.Revision,
            ApproverUserId = access.UserId,
            ApproverNameSnapshot = access.User.Name,
            ApproverGroupId = access.User.GroupId,
            Action = QcApprovalAction.Approve,
            Status = QcApprovalStatus.Approved,
            ApprovedAt = now,
            CreatedAt = now
        };

    private static QcApproval PendingRow(QcInspection x, QcApprovalFlow.Step step) =>
        new()
        {
            InspectionId = x.Id,
            Level = step.Level,
            Revision = x.Revision,
            ApproverGroupId = step.ApproverGroupId,
            Action = "",
            Status = QcApprovalStatus.Pending
        };

    private async Task<string?> ValidateRefsAsync(int? pemborongId, int? mandorId, int? inspectorUserId, CancellationToken ct)
    {
        if (pemborongId != null && !await db.QcPemborong.AnyAsync(x => x.Id == pemborongId && x.Active, ct))
            return "Pemborong tidak ditemukan atau tidak aktif.";
        if (mandorId != null && !await db.QcMandors.AnyAsync(x => x.Id == mandorId && x.Active, ct))
            return "Mandor tidak ditemukan atau tidak aktif.";
        if (inspectorUserId != null && !await db.Users.AnyAsync(x => x.Id == inspectorUserId && x.Active, ct))
            return "Inspektur (user) tidak ditemukan atau tidak aktif.";
        return null;
    }

    private async Task<string?> ResolveInspectorNameAsync(int? inspectorUserId, string? inspectorName, CancellationToken ct)
    {
        var manual = Trim(inspectorName);
        if (manual != null) return manual;
        if (inspectorUserId == null) return null;
        return await db.Users.AsNoTracking().Where(u => u.Id == inspectorUserId)
            .Select(u => u.Name).FirstOrDefaultAsync(ct);
    }

    private async Task<Dictionary<string, string>> CreatorNamesAsync(IEnumerable<string?> logins, CancellationToken ct)
    {
        var keys = logins.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct().ToList();
        if (keys.Count == 0) return new Dictionary<string, string>();
        var rows = await db.Users.AsNoTracking()
            .Where(u => keys.Contains(u.UserId))
            .Select(u => new { u.UserId, u.Name })
            .ToListAsync(ct);
        return rows.GroupBy(u => u.UserId).ToDictionary(g => g.Key, g => g.First().Name);
    }

    private QcInspectionListItemDto BuildListItem(
        QcInspection x, QcAccess access, int activeFindings, int overdueFindings,
        IReadOnlyList<QcApprovalFlow.Step> steps, string? createdByName)
    {
        var step = x.CurrentApprovalLevel > 0 ? steps.FirstOrDefault(s => s.Level == x.CurrentApprovalLevel) : null;
        var approverEligible = step != null && !step.IsCreatorStep
            && access.CanApproveStep(step)
            && (!step.ScopeByAfdeling || access.MatchesAfdeling(x.KodeAfdeling));

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
            x.Revision,
            x.CreatedAt,
            x.SubmittedAt,
            x.ApprovedAt,
            x.ClosedAt,
            x.CreatedBy,
            createdByName,
            activeFindings,
            overdueFindings,
            AllowedActions(x, access, activeFindings, approverEligible));
    }

    private List<string> AllowedActions(QcInspection x, QcAccess access, int activeFindings, bool approverEligible)
    {
        var a = new List<string>();
        var owner = access.IsOwner(x) || access.IsSuperAdmin;

        if (owner && engine.CanEdit(x.Status))
            a.Add("UPDATE");

        if (owner && engine.CanSubmit(x.Status))
            a.Add("SUBMIT");

        if (owner && engine.CanResubmit(x.Status))
            a.Add("RESUBMIT");

        if (engine.CanApprove(x.Status) && approverEligible)
        {
            a.Add("APPROVE");
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
            QcFindingStatus.Active.Contains(f.Status) && f.TargetDate.HasValue && f.TargetDate.Value < today,
            f.CreatedAt);

    private static JsonDocument? ParseData(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonDocument.Parse(json);

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private ObjectResult NoAccess(string action) =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = $"Anda tidak memiliki akses untuk {action}." });
}