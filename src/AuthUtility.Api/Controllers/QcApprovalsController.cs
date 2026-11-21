using System.Security.Claims;
using AuthUtility.Api.Qc;
using AuthUtility.Application.DTOs.Qc;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

// Daftar inspeksi yang menunggu approval oleh user saat ini (level aktif).
// Approver eligibility 100% backend: master step (group / menu) + scope afdeling.
[ApiController]
[Route("api/qc/approvals")]
[Authorize]
public class QcApprovalsController(AppDbContext db, QcApprovalFlow flow) : ControllerBase
{
    private const int MaxListSize = 500;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Metadata alur approval form (versi aktif terbaru); enforcement tetap di endpoint aksi.
    [HttpGet("flow/{formCode}")]
    public async Task<IActionResult> GetFlow(string formCode, CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();
        var form = await db.QcForms.AsNoTracking()
            .Where(x => x.Code == formCode && x.Active)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);
        if (form == null) return NotFound(new { message = "Form QC tidak ditemukan." });
        var steps = await flow.GetStepsAsync(form.Id, ct);
        var levels = steps.Select(s => new
        {
            level = s.Level,
            label = s.Label,
            roleName = s.JabatanKode,
            menuCode = s.MenuCode,
            groupCode = s.ApproverGroupCode,
            scopeByAfdeling = s.ScopeByAfdeling,
            isCreatorStep = s.IsCreatorStep
        }).ToList();
        return Ok(new
        {
            formCode = form.Code,
            formName = form.Name,
            maxLevel = QcApprovalFlow.MaxLevel(steps),
            levels
        });
    }

    // GET /api/qc/approvals/pending
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var candidates = await db.QcInspections.AsNoTracking()
            .Include(x => x.Form)
            .Where(x => QcInspectionStatus.PendingApproval.Contains(x.Status))
            .OrderByDescending(x => x.SubmittedAt)
            .Take(MaxListSize)
            .ToListAsync(ct);

        var stepsMap = await flow.GetStepsForFormsAsync(candidates.Select(x => x.FormId), ct);

        QcApprovalFlow.Step? StepOf(QcInspection x) =>
            stepsMap.TryGetValue(x.FormId, out var steps)
                ? steps.FirstOrDefault(s => s.Level == x.CurrentApprovalLevel)
                : null;

        // Filter: user boleh approve step aktif (group / menu) + scope afdeling bila diminta.
        var visible = candidates
            .Where(x =>
            {
                var step = StepOf(x);
                return step != null && !step.IsCreatorStep
                    && access.CanApproveStep(step)
                    && (!step.ScopeByAfdeling || access.MatchesAfdeling(x.KodeAfdeling));
            })
            .ToList();

        var ids = visible.Select(x => x.Id).ToList();
        var lastActions = await db.QcHistories.AsNoTracking()
            .Where(h => ids.Contains(h.InspectionId))
            .GroupBy(h => h.InspectionId)
            .Select(g => new { InspectionId = g.Key, UserName = g.OrderByDescending(h => h.Id).First().User.Name, At = g.OrderByDescending(h => h.Id).First().CreatedAt })
            .ToDictionaryAsync(x => x.InspectionId, ct);

        var groupIds = visible.Select(x => StepOf(x)!.ApproverGroupId)
            .Where(g => g != null).Select(g => g!.Value).Distinct().ToList();
        var groupNames = groupIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Groups.AsNoTracking().Where(g => groupIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        var items = visible.Select(x =>
        {
            var step = StepOf(x)!;
            var steps = stepsMap[x.FormId];
            return new QcPendingApprovalDto(
                x.Id,
                x.InspectionNo,
                x.FormId,
                x.Form!.Code,
                x.Form.Name,
                x.InspectionDate,
                x.CompanyNameSnapshot,
                x.WilayahKode,
                x.PersilKode,
                x.Commodity,
                x.CurrentApprovalLevel,
                x.Revision,
                x.SubmittedAt ?? x.CreatedAt,
                lastActions.TryGetValue(x.Id, out var la) ? la.UserName : null,
                lastActions.TryGetValue(x.Id, out var laa) ? laa.At : null,
                QcApprovalFlow.MaxLevel(steps),
                step.Label,
                step.JabatanKode,
                step.ApproverGroupId == null ? null : groupNames.GetValueOrDefault(step.ApproverGroupId.Value),
                step.ScopeByAfdeling);
        });

        return Ok(items);
    }
}