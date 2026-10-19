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
    string MenuType,
    int SortOrder,
    LynPermissionDto Permissions
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
    DateTime? UpdatedAt
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

public class CreateLynRequest
{
    public string MenuCode { get; set; } = "";
    public string Title { get; set; } = "";
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
