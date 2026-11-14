namespace AuthUtility.Domain.Entities;

/* =========================================================
   STATUS / ENUM CONSTANTS
   Phase 1: 6 status inspeksi (tanpa SUBMITTED — SUBMIT adalah aksi).
   ========================================================= */

// Status inspeksi QC. Lihat QcStatusEngine untuk transisi legal.
public static class QcInspectionStatus
{
    public const string Draft = "DRAFT";
    public const string WaitingApproval = "WAITING_APPROVAL";
    public const string RevisionRequired = "REVISION_REQUIRED";
    public const string Rejected = "REJECTED";
    public const string Approved = "APPROVED";
    public const string Closed = "CLOSED";

    public static readonly string[] All =
        { Draft, WaitingApproval, RevisionRequired, Rejected, Approved, Closed };

    // Boleh edit DataJson / field umum oleh pembuat: DRAFT & REVISION_REQUIRED.
    public static readonly string[] Editable = { Draft, RevisionRequired };

    // Boleh membuat Finding (temuan adalah bagian hasil pemeriksaan).
    public static readonly string[] FindingCreatable =
        { Draft, WaitingApproval, RevisionRequired, Approved };

    // Terminal: tidak ada transisi keluar (kecuali admin khusus, tidak Phase 1).
    public static readonly string[] Terminal = { Rejected, Closed };
}

// Aksi approval (disimpan di QcApproval.Action).
public static class QcApprovalAction
{
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
    public const string RequestRevision = "REQUEST_REVISION";
}

// Status baris approval per level (QcApproval.Status).
public static class QcApprovalStatus
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string RevisionRequired = "REVISION_REQUIRED";
}

// Severity temuan.
public static class QcFindingSeverity
{
    public const string Minor = "MINOR";
    public const string Major = "MAJOR";
    public const string Critical = "CRITICAL";

    public static readonly string[] All = { Minor, Major, Critical };
}

// Status temuan. Phase 1 pakai: OPEN -> FOLLOW_UP -> VERIFIED -> CLOSED.
// (WAITING_VERUNICATION didefinisikan untuk flow penuh, belum digerakkan endpoint Phase 1.)
public static class QcFindingStatus
{
    public const string Open = "OPEN";
    public const string FollowUp = "FOLLOW_UP";
    public const string WaitingVerification = "WAITING_VERIFICATION";
    public const string Verified = "VERIFIED";
    public const string Closed = "CLOSED";

    public static readonly string[] All =
        { Open, FollowUp, WaitingVerification, Verified, Closed };

    // Status yang masih aktif dan menghalangi inspection CLOSE.
    // Phase 1: seluruh finding harus CLOSED sebelum inspection boleh CLOSED.
    public static readonly string[] Active =
        { Open, FollowUp, WaitingVerification, Verified };
}

// Aksi yang dicatat di QcHistory (audit trail per inspeksi).
public static class QcHistoryAction
{
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Submit = "SUBMIT";
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
    public const string RequestRevision = "REQUEST_REVISION";
    public const string Resubmit = "RESUBMIT";
    public const string Close = "CLOSE";
}

/* =========================================================
   1. QcForm — master jenis pemeriksaan
   ========================================================= */

public class QcForm
{
    public int Id { get; set; }

    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    // KELAPA / NANAS — menentukan prefix nomor & sumber persil.
    public string Commodity { get; set; } = "";

    public string? Description { get; set; }

    public bool Active { get; set; } = true;

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public ICollection<QcInspection> Inspections { get; set; } = new List<QcInspection>();
}

/* =========================================================
   2. QcInspection — SATU tabel transaksi utama untuk semua form
   ========================================================= */

public class QcInspection
{
    public long Id { get; set; }

    public string? InspectionNo { get; set; }

    public int FormId { get; set; }

    public QcForm? Form { get; set; }

    public DateOnly InspectionDate { get; set; }

    // Ref PKBPayroll (cross-db, tanpa FK).
    public int? CompanyId { get; set; }

    public string? CompanyNameSnapshot { get; set; }

    // KodeWilayah dari sumber persil MyPAD/RSUPPKB.
    public string? WilayahKode { get; set; }

    // Sumber persil: KLP / TNM_NNS / LHN_NNS / PNN (lihat PersilReader).
    public string? PersilSumber { get; set; }

    public string? PersilId { get; set; }

    public string? PersilKode { get; set; }

    public string? KodeAfdeling { get; set; }

    public string Commodity { get; set; } = "";

    // Periode untuk reporting (opsional per form).
    public int? PeriodMonth { get; set; }

    public int? PeriodYear { get; set; }

    public string Status { get; set; } = QcInspectionStatus.Draft;

    // Level yang sedang menunggu approval (0 = belum submit).
    public int CurrentApprovalLevel { get; set; }

    // Field spesifik per form (hibrida). Field filter/dashboard tetap kolom relational.
    public string? DataJson { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    // Diisi saat final approval (level terakhir). APPROVED != CLOSED.
    public DateTime? ApprovedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public ICollection<QcApproval> Approvals { get; set; } = new List<QcApproval>();

    public ICollection<QcFinding> Findings { get; set; } = new List<QcFinding>();

    public ICollection<QcHistory> Histories { get; set; } = new List<QcHistory>();
}

/* =========================================================
   3. QcApproval — one-to-many, satu row per (InspectionId, Level)
   ========================================================= */

public class QcApproval
{
    public long Id { get; set; }

    public long InspectionId { get; set; }

    public QcInspection? Inspection { get; set; }

    public int Level { get; set; }

    public int? ApproverUserId { get; set; }

    public UtlUser? Approver { get; set; }

    public string? ApproverNameSnapshot { get; set; }

    public int? ApproverGroupId { get; set; }

    public UtlGroup? ApproverGroup { get; set; }

    // APPROVE / REJECT / REQUEST_REVISION
    public string Action { get; set; } = "";

    // PENDING / APPROVED / REJECTED / REVISION_REQUIRED
    public string Status { get; set; } = QcApprovalStatus.Pending;

    public string? Comment { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

/* =========================================================
   4. QcFinding — temuan, one-to-many per inspeksi
   ========================================================= */

public class QcFinding
{
    public long Id { get; set; }

    public string? FindingNo { get; set; }

    public long InspectionId { get; set; }

    public QcInspection? Inspection { get; set; }

    public string? Category { get; set; }

    public string Description { get; set; } = "";

    // MINOR / MAJOR / CRITICAL
    public string Severity { get; set; } = QcFindingSeverity.Minor;

    public int? PicUserId { get; set; }

    public UtlUser? PicUser { get; set; }

    public string? PicNameSnapshot { get; set; }

    public DateOnly? TargetDate { get; set; }

    // OPEN / FOLLOW_UP / WAITING_VERIFICATION / VERIFIED / CLOSED
    public string Status { get; set; } = QcFindingStatus.Open;

    public string? FollowUpNote { get; set; }

    public int? VerifiedByUserId { get; set; }

    public UtlUser? VerifiedBy { get; set; }

    public DateOnly? VerifiedDate { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/* =========================================================
   5. QcHistory — audit trail (immutable, RESTRICT)
   ========================================================= */

public class QcHistory
{
    public long Id { get; set; }

    public long InspectionId { get; set; }

    public QcInspection? Inspection { get; set; }

    public int? UserId { get; set; }

    public UtlUser? User { get; set; }

    // CREATE / UPDATE / SUBMIT / APPROVE / REJECT / REQUEST_REVISION / RESUBMIT / CLOSE
    public string Action { get; set; } = "";

    public string? FromStatus { get; set; }

    public string? ToStatus { get; set; }

    public int? ApprovalLevel { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
