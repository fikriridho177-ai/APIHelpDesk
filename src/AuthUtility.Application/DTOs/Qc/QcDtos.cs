using System.Text.Json;

namespace AuthUtility.Application.DTOs.Qc;

/* =========================================================
   QC — DTO Phase 1
   Konvensi mengikuti DTOs/Layanan: record untuk response,
   class untuk request body.
   ========================================================= */

/* ---------- Form ---------- */

public record QcFormDto(
    int Id,
    string Code,
    string Name,
    string Commodity,
    string? Description,
    bool Active
);

/* ---------- Inspection: list ---------- */

public record QcInspectionListItemDto(
    long Id,
    string? InspectionNo,
    int FormId,
    string FormCode,
    string FormName,
    DateOnly InspectionDate,
    int? CompanyId,
    string? CompanyNameSnapshot,
    string? WilayahKode,
    string? PersilSumber,
    string? PersilKode,
    string Commodity,
    int? PeriodMonth,
    int? PeriodYear,
    string Status,
    int CurrentApprovalLevel,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    DateTime? ClosedAt,
    string? CreatedBy,
    string? CreatedByName,
    int OpenFindingCount,
    int OverdueFindingCount,
    List<string> AllowedActions
);

public record QcInspectionListResult(
    int Total,
    List<QcInspectionListItemDto> Items
);

/* ---------- Inspection: detail ---------- */

public record QcApprovalDto(
    long Id,
    int Level,
    int? ApproverUserId,
    string? ApproverNameSnapshot,
    int? ApproverGroupId,
    string Action,
    string Status,
    string? Comment,
    DateTime? ApprovedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record QcFindingListItemDto(
    long Id,
    string? FindingNo,
    long InspectionId,
    string? InspectionNo,
    string? Category,
    string Description,
    string Severity,
    string Status,
    int? PicUserId,
    string? PicNameSnapshot,
    DateOnly? TargetDate,
    bool Overdue,
    DateTime CreatedAt
);

public record QcHistoryDto(
    long Id,
    string Action,
    string? FromStatus,
    string? ToStatus,
    int? ApprovalLevel,
    string? Note,
    int? UserId,
    string? UserName,
    DateTime CreatedAt
);

public record QcInspectionDetailDto(
    QcInspectionListItemDto Summary,
    JsonDocument? Data,
    string? KodeAfdeling,
    string? PersilId,
    List<QcApprovalDto> Approvals,
    List<QcFindingListItemDto> Findings,
    List<QcHistoryDto> Histories
);

/* ---------- Inspection: request ---------- */

public class CreateQcInspectionRequest
{
    public string FormCode { get; set; } = "";
    public DateOnly? InspectionDate { get; set; }
    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? WilayahKode { get; set; }
    public string? PersilSumber { get; set; }
    public string? PersilId { get; set; }
    public string? PersilKode { get; set; }
    public string? KodeAfdeling { get; set; }
    public int? PeriodMonth { get; set; }
    public int? PeriodYear { get; set; }
    // Field spesifik form sebagai JSON (object -> disimpan jsonb).
    public JsonDocument? Data { get; set; }
}

public class UpdateQcInspectionRequest
{
    public DateOnly? InspectionDate { get; set; }
    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? WilayahKode { get; set; }
    public string? PersilSumber { get; set; }
    public string? PersilId { get; set; }
    public string? PersilKode { get; set; }
    public string? KodeAfdeling { get; set; }
    public int? PeriodMonth { get; set; }
    public int? PeriodYear { get; set; }
    public JsonDocument? Data { get; set; }
}

// Body aksi approval / submit / resubmit / close.
public class QcInspectionActionRequest
{
    public string? Comment { get; set; }
}

/* ---------- Approval pending ---------- */

public record QcPendingApprovalDto(
    long InspectionId,
    string? InspectionNo,
    int FormId,
    string FormCode,
    string FormName,
    DateOnly InspectionDate,
    string? CompanyNameSnapshot,
    string? WilayahKode,
    string? PersilKode,
    string Commodity,
    int CurrentApprovalLevel,
    DateTime SubmittedAt,
    string? LastActionBy,
    DateTime? LastActionAt,
    int MaxApprovalLevel,
    string ApprovalLabel,
    string? ApprovalRoleName,
    string? RequiredGroupCode
);

/* ---------- Finding: list & detail ---------- */

public record QcFindingDetailDto(
    long Id,
    string? FindingNo,
    long InspectionId,
    string? InspectionNo,
    string InspectionStatus,
    string? Category,
    string Description,
    string Severity,
    string Status,
    int? PicUserId,
    string? PicNameSnapshot,
    DateOnly? TargetDate,
    string? FollowUpNote,
    int? VerifiedByUserId,
    string? VerifiedByName,
    DateOnly? VerifiedDate,
    bool Overdue,
    string? CreatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/* ---------- Finding: request ---------- */

public class CreateQcFindingRequest
{
    public long InspectionId { get; set; }
    public string? Category { get; set; }
    public string Description { get; set; } = "";
    public string Severity { get; set; } = "MINOR";
    public int? PicUserId { get; set; }
    public string? PicName { get; set; }
    public DateOnly? TargetDate { get; set; }
}

public class QcFindingFollowUpRequest
{
    public string? FollowUpNote { get; set; }
}
