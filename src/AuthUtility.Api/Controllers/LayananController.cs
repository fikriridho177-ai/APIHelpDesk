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

// Layanan sesuai flowchart: Pilih Layanan -> Isi Formulir -> Diterima Admin -> Penjadwalan -> Proses -> Update Status -> Selesai.
// Divisi & sub-layanan = baris UtlMenu (route /layanan/...), diatur lewat Manajemen Menu.
// Permission per sub-layanan = Hak Akses > Akses Menu; cakupan data petugas = Hak Akses > Akses Departemen.
[ApiController]
[Route("api/layanan")]
[Authorize]
public class LayananController(AppDbContext db) : ControllerBase
{
    private const int MaxListSize = 500;

    // Aksi -> (status asal yang diizinkan, status tujuan, permission yang dibutuhkan, catatan wajib).
    private record Transition(string[] From, string? To, MenuPermission? Permission, bool NoteRequired);

    private static readonly Dictionary<string, Transition> Transitions = new()
    {
        ["ACCEPT"] = new([LynStatus.Baru], LynStatus.Diterima, MenuPermission.Approve, false),
        ["REJECT"] = new([LynStatus.Baru, LynStatus.Diterima], LynStatus.Ditolak, MenuPermission.Approve, true),
        ["SCHEDULE"] = new([LynStatus.Diterima, LynStatus.Dijadwalkan], LynStatus.Dijadwalkan, MenuPermission.Approve, false),
        ["PROCESS"] = new([LynStatus.Dijadwalkan, LynStatus.MenungguInfo], LynStatus.Diproses, MenuPermission.Update, false),
        ["NEED_INFO"] = new([LynStatus.Diterima, LynStatus.Dijadwalkan, LynStatus.Diproses], LynStatus.MenungguInfo, MenuPermission.Update, true),
        ["FINISH"] = new([LynStatus.Diproses], LynStatus.Selesai, MenuPermission.Update, false),
        // CANCEL: pemohon sendiri (status BARU) atau user dengan permission Delete.
        ["CANCEL"] = new([LynStatus.Baru, LynStatus.Diterima, LynStatus.Dijadwalkan, LynStatus.MenungguInfo], LynStatus.Dibatalkan, MenuPermission.Delete, false),
        // COMMENT: catatan tanpa ubah status, untuk pemohon & petugas.
        ["COMMENT"] = new([], null, null, true),
    };

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /* =========================================================
       DAFTAR LAYANAN
    ========================================================= */

    // Divisi & sub-layanan yang boleh dibuka user (View), beserta permission-nya.
    [HttpGet("services")]
    public async Task<IActionResult> GetServices()
    {
        var access = await LayananAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var visible = access.Menus.Values
            .Where(menu => access.Has(menu.Id, MenuPermission.View))
            .Select(menu => new LynServiceDto(
                menu.Id, menu.ParentId, menu.Code, menu.Name, menu.Route, menu.Icon, menu.MenuType, menu.SortOrder,
                new LynPermissionDto(
                    access.Has(menu.Id, MenuPermission.View),
                    access.Has(menu.Id, MenuPermission.Create),
                    access.Has(menu.Id, MenuPermission.Read),
                    access.Has(menu.Id, MenuPermission.Update),
                    access.Has(menu.Id, MenuPermission.Delete),
                    access.Has(menu.Id, MenuPermission.Approve),
                    access.Has(menu.Id, MenuPermission.Export))))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToList();

        return Ok(visible);
    }

    /* =========================================================
       PENGAJUAN
    ========================================================= */

    // Pengajuan yang boleh dilihat: milik sendiri + (sebagai petugas) sesuai Akses Menu & Akses Departemen.
    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests(string? menuCode, string? status, string? search, bool mine = false)
    {
        var access = await LayananAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var requests = await QueryVisible(access, mine);

        if (!string.IsNullOrWhiteSpace(menuCode))
        {
            // Termasuk sub-layanan di bawahnya (misal menuCode divisi "LYN_RNA").
            var menuIds = GetMenuAndDescendantIds(access, menuCode);
            requests = requests.Where(x => menuIds.Contains(x.MenuId)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(status))
            requests = requests.Where(x => x.Status == status).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            requests = requests.Where(x =>
                x.Title.ToLower().Contains(keyword) ||
                (x.RequestNo ?? "").ToLower().Contains(keyword) ||
                (x.Requester?.Name ?? "").ToLower().Contains(keyword)).ToList();
        }

        return Ok(requests.Take(MaxListSize).Select(x => ToListItem(x, access)).ToList());
    }

    [HttpGet("requests/{id:long}")]
    public async Task<IActionResult> GetRequest(long id)
    {
        var access = await LayananAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var request = await db.LynRequests.AsNoTracking().Include(x => x.Requester).FirstOrDefaultAsync(x => x.Id == id);
        if (request == null || !access.CanSee(request)) return NotFound(new { message = "Pengajuan tidak ditemukan." });

        return Ok(await BuildDetail(request, access));
    }

    [HttpPost("requests")]
    public async Task<IActionResult> CreateRequest(CreateLynRequest body)
    {
        var access = await LayananAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var menu = access.Menus.Values.FirstOrDefault(x => x.Code == body.MenuCode && access.IsService(x));
        if (menu == null) return BadRequest(new { message = "Layanan tidak ditemukan atau tidak aktif." });
        if (!access.Has(menu.Id, MenuPermission.Create)) return NoAccess("mengajukan layanan ini");

        var title = body.Title.Trim();
        var description = body.Description.Trim();
        var priority = body.Priority.Trim().ToUpperInvariant();

        if (title.Length == 0 || title.Length > 200) return BadRequest(new { message = "Judul wajib diisi (maksimal 200 karakter)." });
        if (description.Length == 0) return BadRequest(new { message = "Deskripsi kebutuhan wajib diisi." });
        if (!LynPriority.All.Contains(priority)) return BadRequest(new { message = "Prioritas tidak valid." });

        var request = new LynRequest
        {
            MenuId = menu.Id,
            RequesterUserId = access.UserId,
            RequesterDeptId = access.User.DeptId,
            RequesterCompanyId = access.User.CompanyId,
            Title = title,
            Description = description,
            Location = body.Location?.Trim(),
            NeededDate = body.NeededDate,
            Priority = priority
        };

        await using var tx = await db.Database.BeginTransactionAsync();

        db.LynRequests.Add(request);
        await db.SaveChangesAsync();

        // Nomor pengajuan: LYN-yyMMdd-000123
        request.RequestNo = $"LYN-{DateTime.UtcNow.AddHours(7):yyMMdd}-{request.Id:D6}";

        db.LynRequestLogs.Add(new LynRequestLog
        {
            RequestId = request.Id,
            UserId = access.UserId,
            Action = "CREATE",
            ToStatus = LynStatus.Baru,
            Note = "Formulir pengajuan dikirim."
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        request.Requester = access.User;
        return Ok(await BuildDetail(request, access));
    }

    // Ubah status / tambah catatan sesuai alur flowchart.
    [HttpPost("requests/{id:long}/actions")]
    public async Task<IActionResult> RunAction(long id, LynActionRequest body)
    {
        var access = await LayananAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var request = await db.LynRequests.Include(x => x.Requester).FirstOrDefaultAsync(x => x.Id == id);
        if (request == null || !access.CanSee(request)) return NotFound(new { message = "Pengajuan tidak ditemukan." });

        var action = body.Action.Trim().ToUpperInvariant();
        if (!GetAllowedActions(request, access).Contains(action))
            return NoAccess($"melakukan aksi {action} pada pengajuan berstatus {request.Status}");

        var transition = Transitions[action];
        var note = body.Note?.Trim();
        if (transition.NoteRequired && string.IsNullOrWhiteSpace(note))
            return BadRequest(new { message = "Catatan wajib diisi untuk aksi ini." });

        if (action == "SCHEDULE")
        {
            if (body.ScheduledAt == null) return BadRequest(new { message = "Tanggal jadwal wajib diisi." });
            request.ScheduledAt = body.ScheduledAt.Value.ToUniversalTime();
            request.Assignee = body.Assignee?.Trim();
        }

        var fromStatus = request.Status;

        if (transition.To != null)
        {
            request.Status = transition.To;
            if (LynStatus.Closed.Contains(transition.To)) request.ClosedAt = DateTime.UtcNow;
        }

        request.UpdatedAt = DateTime.UtcNow;

        db.LynRequestLogs.Add(new LynRequestLog
        {
            RequestId = request.Id,
            UserId = access.UserId,
            Action = action,
            FromStatus = fromStatus,
            ToStatus = transition.To,
            Note = action == "SCHEDULE" ? BuildScheduleNote(request, note) : note
        });

        await db.SaveChangesAsync();
        return Ok(await BuildDetail(request, access));
    }

    // Ringkasan jumlah pengajuan per status (untuk dashboard).
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(bool mine = false)
    {
        var access = await LayananAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var requests = await QueryVisible(access, mine);
        int Count(params string[] statuses) => requests.Count(x => statuses.Contains(x.Status));

        return Ok(new LynSummaryDto(
            Count(LynStatus.Baru),
            Count(LynStatus.Diterima, LynStatus.Dijadwalkan, LynStatus.Diproses),
            Count(LynStatus.MenungguInfo),
            Count(LynStatus.Selesai),
            Count(LynStatus.Ditolak, LynStatus.Dibatalkan),
            requests.Count));
    }

    /* =========================================================
       HELPER
    ========================================================= */

    private async Task<List<LynRequest>> QueryVisible(LayananAccess access, bool mine)
    {
        var me = access.UserId;
        var handlerMenuIds = mine
            ? new List<int>()
            : access.Menus.Values.Where(m => access.IsService(m) && access.IsHandler(m.Id)).Select(m => m.Id).ToList();

        var candidates = await db.LynRequests
            .AsNoTracking()
            .Include(x => x.Requester)
            .Where(x => x.RequesterUserId == me || handlerMenuIds.Contains(x.MenuId))
            .OrderByDescending(x => x.Id)
            .ToListAsync();

        // Filter Akses Departemen (DeptId pemohon disimpan sebagai teks, dibandingkan sebagai angka).
        return candidates.Where(access.CanSee).ToList();
    }

    private static HashSet<int> GetMenuAndDescendantIds(LayananAccess access, string menuCode)
    {
        var root = access.Menus.Values.FirstOrDefault(x => x.Code == menuCode);
        if (root == null) return new HashSet<int>();

        var ids = new HashSet<int> { root.Id };
        bool added;
        do
        {
            added = false;
            foreach (var menu in access.Menus.Values)
                if (menu.ParentId.HasValue && ids.Contains(menu.ParentId.Value) && ids.Add(menu.Id))
                    added = true;
        } while (added);

        return ids;
    }

    private static List<string> GetAllowedActions(LynRequest request, LayananAccess access)
    {
        var isOwner = request.RequesterUserId == access.UserId;
        var allowed = new List<string>();

        foreach (var (action, transition) in Transitions)
        {
            if (action == "COMMENT")
            {
                if (!LynStatus.Closed.Contains(request.Status)) allowed.Add(action);
                continue;
            }

            if (!transition.From.Contains(request.Status)) continue;

            var permitted = access.Has(request.MenuId, transition.Permission!.Value) &&
                            access.InDepartmentScope(request.RequesterDeptId);

            // Pemohon boleh membatalkan pengajuannya sendiri selama belum diterima.
            if (action == "CANCEL" && isOwner && request.Status == LynStatus.Baru) permitted = true;

            if (permitted) allowed.Add(action);
        }

        return allowed;
    }

    private LynRequestListItemDto ToListItem(LynRequest x, LayananAccess access)
    {
        access.Menus.TryGetValue(x.MenuId, out var menu);

        return new LynRequestListItemDto(
            x.Id,
            x.RequestNo,
            x.MenuId,
            menu?.Code ?? "",
            menu?.Name ?? "(layanan nonaktif)",
            access.GetDivisionName(x.MenuId),
            x.Title,
            x.Priority,
            x.Status,
            x.RequesterUserId,
            x.Requester?.Name ?? "",
            x.RequesterDeptId,
            x.CreatedAt,
            x.ScheduledAt,
            x.UpdatedAt);
    }

    private async Task<LynRequestDetailDto> BuildDetail(LynRequest request, LayananAccess access)
    {
        var logs = await db.LynRequestLogs
            .AsNoTracking()
            .Where(x => x.RequestId == request.Id)
            .OrderBy(x => x.Id)
            .Select(x => new LynRequestLogDto(x.Id, x.Action, x.FromStatus, x.ToStatus, x.Note, x.User!.Name, x.CreatedAt))
            .ToListAsync();

        return new LynRequestDetailDto(
            ToListItem(request, access),
            request.Description,
            request.Location,
            request.NeededDate,
            request.Assignee,
            request.ClosedAt,
            request.Requester?.UserId ?? "",
            request.RequesterCompanyId,
            GetAllowedActions(request, access),
            logs);
    }

    private static string BuildScheduleNote(LynRequest request, string? note)
    {
        var schedule = $"Dijadwalkan {request.ScheduledAt!.Value.AddHours(7):dd/MM/yyyy HH:mm} WIB";
        if (!string.IsNullOrWhiteSpace(request.Assignee)) schedule += $", petugas: {request.Assignee}";
        return string.IsNullOrWhiteSpace(note) ? schedule + "." : $"{schedule}. {note}";
    }

    private ObjectResult NoAccess(string action) =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = $"Anda tidak memiliki akses untuk {action}." });
}
