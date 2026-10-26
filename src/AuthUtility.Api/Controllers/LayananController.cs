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

        var jenisCounts = await db.LynJenisPermintaan
            .AsNoTracking()
            .Where(x => x.Active)
            .GroupBy(x => x.DivisionMenuId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var visible = access.Menus.Values
            .Where(menu => access.Has(menu.Id, MenuPermission.View) && !menu.Code.StartsWith(LayananAccess.PageCodePrefix))
            .Select(menu => new LynServiceDto(
                menu.Id, menu.ParentId, menu.Code, menu.Name, menu.Route, menu.Icon,
                menu.FullName, menu.Description, menu.Color, menu.MenuType, menu.SortOrder,
                new LynPermissionDto(
                    access.Has(menu.Id, MenuPermission.View),
                    access.Has(menu.Id, MenuPermission.Create),
                    access.Has(menu.Id, MenuPermission.Read),
                    access.Has(menu.Id, MenuPermission.Update),
                    access.Has(menu.Id, MenuPermission.Delete),
                    access.Has(menu.Id, MenuPermission.Approve),
                    access.Has(menu.Id, MenuPermission.Export)),
                jenisCounts.GetValueOrDefault(menu.Id)))
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
                (x.Requester?.Name ?? "").ToLower().Contains(keyword) ||
                (x.RequesterName ?? "").ToLower().Contains(keyword) ||
                (x.Location ?? "").ToLower().Contains(keyword)).ToList();
        }

        var queue = await LoadQueuePositions(access);
        return Ok(requests.Take(MaxListSize).Select(x => ToListItem(x, access, queue)).ToList());
    }

    [HttpGet("requests/{id:long}")]
    public async Task<IActionResult> GetRequest(long id)
    {
        var access = await LayananAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var request = await db.LynRequests.AsNoTracking().Include(x => x.Requester).Include(x => x.Jenis).FirstOrDefaultAsync(x => x.Id == id);
        if (request == null || !access.CanSee(request)) return NotFound(new { message = "Pengajuan tidak ditemukan." });

        return Ok(await BuildDetail(request, access));
    }

    // Data akun login untuk mengisi awal bagian pemohon di form pengajuan.
    [HttpGet("requester-profile")]
    public async Task<IActionResult> GetRequesterProfile()
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == CurrentUserId);
        if (user == null) return Forbid();

        return Ok(new LynRequesterProfileDto(user.Name, user.Email, user.Phone, user.DeptId, user.CompanyId));
    }

    [HttpPost("requests")]
    public async Task<IActionResult> CreateRequest(CreateLynRequest body)
    {
        var access = await LayananAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        // Jenis Permintaan dari master -> tiket masuk ke divisi jenis tersebut.
        // Tanpa JenisId: MenuCode boleh berisi kode jenis, atau kode sub-layanan (cara lama).
        LynJenisPermintaan? jenis = null;
        if (body.JenisId != null)
        {
            jenis = await db.LynJenisPermintaan.AsNoTracking().FirstOrDefaultAsync(x => x.Id == body.JenisId && x.Active);
            if (jenis == null) return BadRequest(new { message = "Jenis permintaan tidak ditemukan atau tidak aktif." });
        }
        else if (!string.IsNullOrWhiteSpace(body.MenuCode))
        {
            var code = body.MenuCode.Trim();
            jenis = await db.LynJenisPermintaan.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code && x.Active);
        }

        var menu = jenis != null
            ? access.Menus.GetValueOrDefault(jenis.DivisionMenuId)
            : access.Menus.Values.FirstOrDefault(x => x.Code == body.MenuCode && access.IsService(x));
        if (menu == null) return BadRequest(new { message = "Layanan tidak ditemukan atau tidak aktif." });
        if (!access.Has(menu.Id, MenuPermission.Create)) return NoAccess("mengajukan layanan ini");

        static string Clean(string? value) => (value ?? "").Trim();

        var otherType = Clean(body.OtherType);
        var description = Clean(body.Description);
        var location = Clean(body.Location);
        var priority = Clean(body.Priority).ToUpperInvariant();
        var companies = LynRequestRules.JoinCompanies(body.Companies, body.OtherCompany);

        // Isian wajib sesuai form permintaan (tanda *).
        var required = new (string Value, string Label, int Max)[]
        {
            (Clean(body.RequesterName), "Nama Pemohon", 150),
            (Clean(body.RequesterPosition), "Jabatan Pemohon", 100),
            (Clean(body.RequesterDepartment), "Departemen", 150),
            (companies, "Perusahaan", 300),
            (Clean(body.RequesterPhone), "Nomor HP/Whatsapp", 50),
            (Clean(body.RequesterEmail), "Email", 150),
            (description, "Deskripsi Permintaan", int.MaxValue),
            (location, "Lokasi Pekerjaan", 200),
        };
        foreach (var (value, label, max) in required)
        {
            if (value.Length == 0) return BadRequest(new { message = $"{label} wajib diisi." });
            if (value.Length > max) return BadRequest(new { message = $"{label} maksimal {max} karakter." });
        }

        var isOther = jenis?.IsOther ?? menu.Code.EndsWith("_LAINNYA", StringComparison.OrdinalIgnoreCase);
        if (isOther && otherType.Length == 0)
            return BadRequest(new { message = "Jenis permintaan lainnya wajib diisi." });
        if (otherType.Length > 150) return BadRequest(new { message = "Jenis permintaan lainnya maksimal 150 karakter." });
        if (!Clean(body.RequesterEmail).Contains('@')) return BadRequest(new { message = "Format email tidak valid." });
        if (!LynPriority.All.Contains(priority)) return BadRequest(new { message = "Skala prioritas tidak valid." });
        if (body.NeededDate == null) return BadRequest(new { message = "Tanggal Deadline wajib diisi." });

        var title = Clean(body.Title);
        if (title.Length == 0) title = LynRequestRules.BuildTitle(jenis?.Name ?? menu.Name, otherType, location);
        if (title.Length > LynRequestRules.TitleMaxLength) return BadRequest(new { message = "Judul maksimal 200 karakter." });

        var request = new LynRequest
        {
            MenuId = menu.Id,
            JenisId = jenis?.Id,
            RequesterUserId = access.UserId,
            // DeptId & CompanyId akun tetap disimpan untuk filter Akses Departemen.
            RequesterDeptId = access.User.DeptId,
            RequesterCompanyId = access.User.CompanyId,
            RequesterName = Clean(body.RequesterName),
            RequesterPosition = Clean(body.RequesterPosition),
            RequesterDepartment = Clean(body.RequesterDepartment),
            RequesterCompanies = companies,
            RequesterPhone = Clean(body.RequesterPhone),
            RequesterEmail = Clean(body.RequesterEmail),
            OtherType = otherType.Length == 0 ? null : otherType,
            Title = title,
            Description = description,
            Location = location,
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
        request.Jenis = jenis;
        return Ok(await BuildDetail(request, access));
    }

    // Ubah status / tambah catatan sesuai alur flowchart.
    [HttpPost("requests/{id:long}/actions")]
    public async Task<IActionResult> RunAction(long id, LynActionRequest body)
    {
        var access = await LayananAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var request = await db.LynRequests.Include(x => x.Requester).Include(x => x.Jenis).FirstOrDefaultAsync(x => x.Id == id);
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
       PORTAL (widget Antrian & KPI Divisi)
    ========================================================= */

    // Tiket orang lain yang menunggu langkah berikutnya dari user (Terima / Jadwalkan / Proses).
    [HttpGet("queue")]
    public async Task<IActionResult> GetQueue(int take = 10)
    {
        var access = await LayananAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var candidates = (await QueryVisible(access, mine: false))
            .Select(x => new QueueCandidate(x, GetAllowedActions(x, access)));

        var (total, items) = PortalCalculator.SelectQueue(candidates, access.UserId, Math.Clamp(take, 1, 50));
        var queue = await LoadQueuePositions(access);

        return Ok(new LynQueueDto(total, items.Select(x => ToListItem(x, access, queue)).ToList()));
    }

    // Jumlah tiket per divisi dalam N hari terakhir (mengikuti hak lihat user).
    [HttpGet("kpi")]
    public async Task<IActionResult> GetKpi(int days = 30)
    {
        var access = await LayananAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        days = Math.Clamp(days, 1, 365);
        var rows = (await QueryVisible(access, mine: false))
            .Select(x => (x, access.GetDivisionName(x.MenuId)));

        var divisions = PortalCalculator.ComputeKpi(rows, DateTime.UtcNow.AddDays(-days))
            .Select(x => new LynDivisionKpiDto(x.Division, x.Masuk, x.Terbuka, x.Selesai, x.RataHariSelesai))
            .ToList();

        return Ok(new LynKpiDto(days, divisions));
    }

    /* =========================================================
       MONITORING & ANALISIS PERMINTAAN
    ========================================================= */

    // Ringkasan + baris tiket untuk halaman Monitoring / Analisis dan export Excel / PDF.
    // Data mengikuti hak lihat yang sama dengan Daftar Pengajuan (milik sendiri + petugas sesuai Akses Menu & Departemen).
    [HttpGet("report")]
    public async Task<IActionResult> GetReport(DateOnly? dateFrom, DateOnly? dateTo, string? divisionCode, int? jenisId, string? status)
    {
        var access = await LayananAccess.LoadAsync(db, CurrentUserId);
        if (access == null) return Forbid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var from = dateFrom ?? today.AddDays(-89);
        var to = dateTo ?? today;
        if (to < from) (from, to) = (to, from);

        // Batas tanggal dalam WIB -> UTC.
        var fromUtc = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);

        var requests = (await QueryVisible(access, mine: false))
            .Where(x => x.CreatedAt >= fromUtc && x.CreatedAt < toUtc)
            .ToList();

        if (!string.IsNullOrWhiteSpace(divisionCode))
        {
            var menuIds = GetMenuAndDescendantIds(access, divisionCode.Trim());
            requests = requests.Where(x => menuIds.Contains(x.MenuId)).ToList();
        }

        if (jenisId != null) requests = requests.Where(x => x.JenisId == jenisId).ToList();
        if (!string.IsNullOrWhiteSpace(status)) requests = requests.Where(x => x.Status == status.Trim()).ToList();

        var queue = await LoadQueuePositions(access);

        var rows = requests
            .Select(x => new ReportRow(x, access.GetDivisionName(x.MenuId) ?? PortalCalculator.OtherDivision, JenisName(x, access, withOther: false)))
            .ToList();

        var result = LynReportCalculator.Build(rows, today);

        var items = rows.Select(r =>
        {
            var x = r.Request;
            return new LynReportRowDto(
                x.Id, x.RequestNo, r.Division, JenisName(x, access), x.Title, x.Description, x.Location, x.Priority, x.Status,
                string.IsNullOrWhiteSpace(x.RequesterName) ? x.Requester?.Name ?? "" : x.RequesterName,
                x.RequesterPosition, x.RequesterDepartment, x.RequesterCompanies, x.RequesterPhone,
                x.CreatedAt, x.NeededDate, x.ScheduledAt, x.ClosedAt, x.Assignee,
                queue.TryGetValue(x.Id, out var no) ? no : null,
                !LynStatus.Closed.Contains(x.Status) && x.NeededDate.HasValue && x.NeededDate.Value < today,
                x.Status == LynStatus.Selesai && x.ClosedAt.HasValue
                    ? Math.Round((x.ClosedAt.Value - x.CreatedAt).TotalDays, 1, MidpointRounding.AwayFromZero)
                    : null);
        }).ToList();

        return Ok(new
        {
            dateFrom = from,
            dateTo = to,
            result.Total,
            result.PerDivisi,
            result.PerJenis,
            result.PerPrioritas,
            result.PerBulan,
            rows = items
        });
    }

    /* =========================================================
       HELPER
    ========================================================= */

    private async Task<List<LynRequest>> QueryVisible(LayananAccess access, bool mine)
    {
        var me = access.UserId;
        // Menu divisi (tiket dengan jenis permintaan) maupun sub-layanan (tiket model lama).
        var handlerMenuIds = mine
            ? new List<int>()
            : access.Menus.Values
                .Where(m => !m.Code.StartsWith(LayananAccess.PageCodePrefix) && access.IsHandler(m.Id))
                .Select(m => m.Id)
                .ToList();

        var candidates = await db.LynRequests
            .AsNoTracking()
            .Include(x => x.Requester)
            .Include(x => x.Jenis)
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

    // Nomor antrian semua tiket yang masih antri (dihitung dari seluruh tiket divisi, bukan hanya yang terlihat user).
    private async Task<Dictionary<long, int>> LoadQueuePositions(LayananAccess access)
    {
        var waiting = await db.LynRequests
            .AsNoTracking()
            .Where(x => LynStatus.Waiting.Contains(x.Status))
            .Select(x => new { x.Id, x.MenuId, x.Status, x.Priority, x.CreatedAt })
            .ToListAsync();

        return LynRequestRules.QueuePositions(
            waiting.Select(x => new QueueEntry(x.Id, access.GetDivisionKey(x.MenuId), x.Status, x.Priority, x.CreatedAt)));
    }

    // Nama jenis permintaan: dari master jenis, atau nama sub-layanan untuk tiket model lama.
    // withOther = tambahkan isian "Lainnya" (untuk baris tiket); tanpa isian untuk pengelompokan analisis.
    private static string JenisName(LynRequest x, LayananAccess access, bool withOther = true)
    {
        var name = x.Jenis?.Name ?? access.Menus.GetValueOrDefault(x.MenuId)?.Name ?? "(layanan nonaktif)";
        return !withOther || string.IsNullOrWhiteSpace(x.OtherType) ? name : $"{name}: {x.OtherType}";
    }

    private LynRequestListItemDto ToListItem(LynRequest x, LayananAccess access, IReadOnlyDictionary<long, int>? queue = null)
    {
        access.Menus.TryGetValue(x.MenuId, out var menu);

        return new LynRequestListItemDto(
            x.Id,
            x.RequestNo,
            x.MenuId,
            x.Jenis?.Code ?? menu?.Code ?? "",
            x.Jenis?.Name ?? menu?.Name ?? "(layanan nonaktif)",
            access.GetDivisionName(x.MenuId),
            x.Title,
            x.Priority,
            x.Status,
            x.RequesterUserId,
            string.IsNullOrWhiteSpace(x.RequesterName) ? x.Requester?.Name ?? "" : x.RequesterName,
            x.RequesterDeptId,
            x.CreatedAt,
            x.ScheduledAt,
            x.UpdatedAt,
            x.JenisId,
            queue != null && queue.TryGetValue(x.Id, out var no) ? no : null,
            GetAllowedActions(x, access));
    }

    private async Task<LynRequestDetailDto> BuildDetail(LynRequest request, LayananAccess access)
    {
        var logs = await db.LynRequestLogs
            .AsNoTracking()
            .Where(x => x.RequestId == request.Id)
            .OrderBy(x => x.Id)
            .Select(x => new LynRequestLogDto(x.Id, x.Action, x.FromStatus, x.ToStatus, x.Note, x.User!.Name, x.CreatedAt))
            .ToListAsync();

        var queue = await LoadQueuePositions(access);

        return new LynRequestDetailDto(
            ToListItem(request, access, queue),
            request.Description,
            request.Location,
            request.NeededDate,
            request.Assignee,
            request.ClosedAt,
            request.Requester?.UserId ?? "",
            request.RequesterCompanyId,
            request.RequesterPosition,
            request.RequesterDepartment,
            request.RequesterCompanies,
            request.RequesterPhone,
            request.RequesterEmail,
            request.OtherType,
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
