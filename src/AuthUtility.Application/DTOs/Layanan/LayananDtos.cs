namespace AuthUtility.Application.DTOs.Layanan;

/* ---------- Response ---------- */

// Permission user pada satu sub-layanan (dari Hak Akses > Akses Menu).
public record LynPermissionDto(
    bool CanView,
    bool CanCreate,
    bool CanRead,
    bool CanUpdate,
    bool CanDelete,
    bool CanApprove,
    bool CanExport
);

// Divisi / sub-layanan yang boleh diakses user (dibentuk jadi tree di frontend).
public record LynServiceDto(
    int Id,
    int? ParentId,
    string Code,
    string Name,
    string? Route,
    string? Icon,
    string? FullName,
    string? Description,
    string? Color,
    string MenuType,
    int SortOrder,
    LynPermissionDto Permissions,
    // Jumlah jenis permintaan aktif (hanya untuk menu divisi).
    int JenisCount
);

public record LynRequestListItemDto(
    long Id,
    string? RequestNo,
    int MenuId,
    string ServiceCode,
    string ServiceName,
    string? DivisionName,
    string Title,
    string Priority,
    string Status,
    int RequesterUserId,
    string RequesterName,
    string? RequesterDeptId,
    DateTime CreatedAt,
    DateTime? ScheduledAt,
    DateTime? UpdatedAt,
    int? JenisId,
    // Nomor antrian di divisinya (hanya tiket Baru / Disetujui / Dijadwalkan).
    int? QueueNo,
    List<string> AllowedActions
);

public record LynRequestLogDto(
    long Id,
    string Action,
    string? FromStatus,
    string? ToStatus,
    string? Note,
    string UserName,
    DateTime CreatedAt
);

public record LynRequestDetailDto(
    LynRequestListItemDto Summary,
    string Description,
    string? Location,
    DateOnly? NeededDate,
    string? Assignee,
    DateTime? ClosedAt,
    string RequesterUserLogin,
    int? RequesterCompanyId,
    string? RequesterPosition,
    string? RequesterDepartment,
    string? RequesterCompanies,
    string? RequesterPhone,
    string? RequesterEmail,
    string? OtherType,
    List<string> AllowedActions,
    List<LynRequestLogDto> Logs
);

public record LynSummaryDto(
    int Baru,
    int DalamProses,
    int MenungguInfo,
    int Selesai,
    int Ditolak,
    int Total
);

/* ---------- Request ---------- */

// Data akun login untuk mengisi awal bagian "pemohon" di form.
public record LynRequesterProfileDto(string Name, string Email, string? Phone, string? DeptId, int? CompanyId);

// Isian form pengajuan (mengikuti form permintaan GIS).
public class CreateLynRequest
{
    // Jenis Permintaan dari master (LynJenisPermintaan). Divisi tujuan = divisi jenis tersebut.
    public int? JenisId { get; set; }
    // Cara lama: kode sub-layanan atau kode jenis. Dipakai bila JenisId kosong.
    public string? MenuCode { get; set; }
    // Isian "Yang lain" (wajib bila jenis "Lainnya").
    public string? OtherType { get; set; }

    public string RequesterName { get; set; } = "";
    public string RequesterPosition { get; set; } = "";
    public string RequesterDepartment { get; set; } = "";
    public List<string> Companies { get; set; } = new();
    public string? OtherCompany { get; set; }
    public string RequesterPhone { get; set; } = "";
    public string RequesterEmail { get; set; } = "";

    // Opsional: bila kosong dibuat otomatis dari jenis permintaan + lokasi.
    public string? Title { get; set; }
    public string Description { get; set; } = "";
    public string? Location { get; set; }
    public DateOnly? NeededDate { get; set; }
    public string Priority { get; set; } = "NORMAL";
}

public class LynActionRequest
{
    // ACCEPT, REJECT, SCHEDULE, PROCESS, NEED_INFO, FINISH, CANCEL, COMMENT
    public string Action { get; set; } = "";
    public string? Note { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string? Assignee { get; set; }
}

/* ---------- Master Jenis Permintaan ---------- */

public record LynJenisDto(
    int Id,
    int DivisionMenuId,
    string DivisionCode,
    string DivisionName,
    string Code,
    string Name,
    string? Description,
    bool IsOther,
    int SortOrder,
    bool Active,
    // Jumlah tiket yang memakai jenis ini (jenis terpakai hanya bisa dinonaktifkan).
    int UsedCount
);

// Divisi yang bisa dipilih. CanManage = boleh menambah/mengubah jenis divisi ini.
public record LynDivisionOptionDto(int Id, string Code, string Name, bool CanManage);

public record LynJenisListDto(List<LynDivisionOptionDto> Divisions, List<LynJenisDto> Items);

public class SaveLynJenisRequest
{
    public int DivisionMenuId { get; set; }
    // Kosong = dibuat dari nama.
    public string? Code { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsOther { get; set; }
    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;
}

/* ---------- Monitoring & Analisis ---------- */

// Satu baris tiket untuk tabel & export Excel/PDF.
public record LynReportRowDto(
    long Id,
    string? RequestNo,
    string DivisionName,
    string JenisName,
    string Title,
    string Description,
    string? Location,
    string Priority,
    string Status,
    string RequesterName,
    string? RequesterPosition,
    string? RequesterDepartment,
    string? RequesterCompanies,
    string? RequesterPhone,
    DateTime CreatedAt,
    DateOnly? NeededDate,
    DateTime? ScheduledAt,
    DateTime? ClosedAt,
    string? Assignee,
    int? QueueNo,
    bool Terlambat,
    double? HariSelesai
);

/* ---------- Portal ---------- */

// Widget Antrian: tiket yang menunggu tindakan user (Total = semua, Items = maksimal take).
public record LynQueueDto(int Total, List<LynRequestListItemDto> Items);

public record LynDivisionKpiDto(string Division, int Masuk, int Terbuka, int Selesai, double? RataHariSelesai);

// Widget KPI Divisi: tiket yang dibuat dalam Days hari terakhir.
public record LynKpiDto(int Days, List<LynDivisionKpiDto> Divisions);
