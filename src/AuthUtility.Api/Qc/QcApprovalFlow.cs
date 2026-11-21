using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Qc;

// Alur approval per form dari master MstQcFormApprovalStep (bukan appsettings).
// Step aktif diurutkan Level; baris IsCreatorStep = pembuat dokumen (otomatis
// saat submit). Bila form belum punya step aktif: fallback satu level manual
// "APPROVED OLEH" lewat menu QC_APPROVAL agar inspeksi tetap bisa diproses.
public sealed class QcApprovalFlow(AppDbContext db)
{
    public sealed record Step(
        int Level,
        string Label,
        string? JabatanKode,
        int? ApproverGroupId,
        string? ApproverGroupCode,
        string MenuCode,
        bool ScopeByAfdeling,
        bool IsCreatorStep);

    public static readonly Step FallbackStep =
        new(1, "APPROVED OLEH", null, null, null, "QC_APPROVAL", false, false);

    public async Task<IReadOnlyList<Step>> GetStepsAsync(int formId, CancellationToken ct = default)
    {
        var map = await GetStepsForFormsAsync(new[] { formId }, ct);
        return map.TryGetValue(formId, out var steps) ? steps : new[] { FallbackStep };
    }

    // Sekali query untuk banyak form (hindari N+1 di daftar inspeksi/antrean).
    public async Task<Dictionary<int, IReadOnlyList<Step>>> GetStepsForFormsAsync(
        IEnumerable<int> formIds, CancellationToken ct = default)
    {
        var ids = formIds.Distinct().ToList();
        var rows = await db.QcFormApprovalSteps.AsNoTracking()
            .Where(x => ids.Contains(x.FormId) && x.Active)
            .OrderBy(x => x.Level)
            .ToListAsync(ct);

        var groupIds = rows.Where(x => x.ApproverGroupId != null)
            .Select(x => x.ApproverGroupId!.Value).Distinct().ToList();
        var groups = groupIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Groups.AsNoTracking().Where(g => groupIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.Code, ct);

        var result = new Dictionary<int, IReadOnlyList<Step>>();
        foreach (var formId in ids)
        {
            var steps = rows.Where(x => x.FormId == formId)
                .Select(x => new Step(
                    x.Level, x.Label, x.JabatanKode, x.ApproverGroupId,
                    x.ApproverGroupId == null ? null : groups.GetValueOrDefault(x.ApproverGroupId.Value),
                    x.MenuCode, x.ScopeByAfdeling, x.IsCreatorStep))
                .ToList();
            result[formId] = steps.Count > 0 ? steps : new[] { FallbackStep };
        }
        return result;
    }

    public static Step? NextAfter(IReadOnlyList<Step> steps, int level) =>
        steps.FirstOrDefault(x => x.Level > level);

    public static int MaxLevel(IReadOnlyList<Step> steps) => steps.Max(x => x.Level);
}