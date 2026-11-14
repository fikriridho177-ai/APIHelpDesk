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
// Approver eligibility 100% backend (config level + group).
[ApiController]
[Route("api/qc/approvals")]
[Authorize]
public class QcApprovalsController(AppDbContext db, QcApprovalFlowConfig flow) : ControllerBase
{
    private const int MaxListSize = 500;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Metadata only; approval eligibility remains enforced by the existing action endpoints.
    [HttpGet("flow/{formCode}")]
    public async Task<IActionResult> GetFlow(string formCode, CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();
        var form = await db.QcForms.AsNoTracking().FirstOrDefaultAsync(x => x.Code == formCode, ct);
        if (form == null) return NotFound(new { message = "Form QC tidak ditemukan." });
        var levels = flow.GetLevels(form.Code);
        return Ok(new { formCode = form.Code, formName = form.Name, maxLevel = flow.MaxLevel(form.Code), levels });
    }

    // GET /api/qc/approvals/pending
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(CancellationToken ct)
    {
        var access = await QcAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var candidates = await db.QcInspections.AsNoTracking()
            .Include(x => x.Form)
            .Where(x => x.Status == QcInspectionStatus.WaitingApproval)
            .OrderByDescending(x => x.SubmittedAt)
            .Take(MaxListSize)
            .ToListAsync(ct);

        // Filter: user boleh approve level aktif (config MenuCode + GroupCode).
        var visible = candidates
            .Where(x =>
            {
                var cfg = flow.GetLevel(x.Form.Code, x.CurrentApprovalLevel);
                return cfg != null && access.CanApproveOn(cfg.MenuCode, cfg.GroupCode);
            })
            .ToList();

        var ids = visible.Select(x => x.Id).ToList();
        var lastActions = await db.QcHistories.AsNoTracking()
            .Where(h => ids.Contains(h.InspectionId))
            .GroupBy(h => h.InspectionId)
            .Select(g => new { InspectionId = g.Key, UserName = g.OrderByDescending(h => h.Id).First().User.Name, At = g.OrderByDescending(h => h.Id).First().CreatedAt })
            .ToDictionaryAsync(x => x.InspectionId, ct);

        var items = visible.Select(x =>
        {
            var cfg = flow.GetLevel(x.Form!.Code, x.CurrentApprovalLevel)!;
            return new QcPendingApprovalDto(
            x.Id,
            x.InspectionNo,
            x.FormId,
            x.Form.Code,
            x.Form.Name,
            x.InspectionDate,
            x.CompanyNameSnapshot,
            x.WilayahKode,
            x.PersilKode,
            x.Commodity,
            x.CurrentApprovalLevel,
            x.SubmittedAt ?? x.CreatedAt,
            lastActions.TryGetValue(x.Id, out var la) ? la.UserName : null,
            lastActions.TryGetValue(x.Id, out var laa) ? laa.At : null,
            flow.MaxLevel(x.Form.Code),
            cfg.Label,
            cfg.RoleName,
            cfg.GroupCode);
        });

        return Ok(items);
    }
}
