namespace AuthUtility.Domain.Entities;

/* =========================================================
   STATUS / ENUM CONSTANTS
   Fase 2: DRAFT -> SUBMITTED -> IN_REVIEW -> APPROVED -> CLOSED,
   dengan siklus REVISION (pengembalian oleh approver, bukan reject).
   ========================================================= */

// Status inspeksi QC. Lihat QcStatusEngine untuk transisi legal.
public static class QcInspectionStatus
{
    public const string Draft = "DRAFT";
    // Menunggu review pertama setelah submit (baris pembuat otomatis approved).
    public const string Submitted = "SUBMITTED";
    // Sudah melewati minimal satu review approver, masih berjenjang.
    public const string InReview = "IN_REVIEW";
    // Dikembalikan approver untuk diperbaiki pembuat (catatan wajib).
    public const string Revision = "REVISION";
    public const string Approved = "APPROVED";
    public const string Closed = "CLOSED";

    public static readonly string[] All =
        { Draft, Submitted, InReview, Revision, Approved, Closed };

    // Boleh edit DataJson / field umum oleh pembuat: DRAFT & REVISION.
    public static readonly string[] Editable = { Draft, Revision };

    // Boleh membuat Finding (temuan adalah bagian hasil pemeriksaan).
    public static readonly string[] FindingCreatable =
        { Draft, Submitted, InReview, Revision, Approved };

    // Terminal: tidak ada transisi keluar.
    public static readonly string[] Terminal = { Closed };

    // Sedang menunggu approval (antrean approver).
    public static readonly string[] PendingApproval = { Submitted, InReview };
}

// Aksi approval (disimpan di QcApproval.Action).
// Pengembalian untuk revisi menggantikan reject + request-revision lama.
public static class QcApprovalAction
{
    public const string Approve = "APPROVE";
    public const string RequestRevision = "REQUEST_REVISION";
}

// Status baris approval per level (QcApproval.Status).
public static class QcApprovalStatus
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
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

// Status temuan. Fase 2: OPEN -> IN_PROGRESS -> DONE -> VERIFIED.
// VERIFIED = terminal; tidak ada status CLOSED terpisah lagi.
public static class QcFindingStatus
{
    public const string Open = "OPEN";
    public const string InProgress = "IN_PROGRESS";
    public const string Done = "DONE";
    public const string Verified = "VERIFIED";

    public static readonly string[] All = { Open, InProgress, Done, Verified };

    // Status yang masih aktif dan menghalangi inspection CLOSE.
    public static readonly string[] Active = { Open, InProgress, Done };
}

// Aksi yang dicatat di QcHistory (audit trail per inspeksi).
public static class QcHistoryAction
{
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Submit = "SUBMIT";
    public const string Approve = "APPROVE";
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

    // Versi form: satu Code boleh punya beberapa versi; inspeksi lama tetap merujuk versinya.
    public int Version { get; set; } = 1;

    public DateOnly? EffectiveFrom { get; set; }

    // Pola detail: A sampel jalur, B dosis pupuk, C inventaris, D pengiriman, E rekap bulanan.
    public string? Pattern { get; set; }

    public string? TemplateFile { get; set; }

    public string? PrintTitle { get; set; }

    // Kode form asli pada file Excel (FRM-QC-...); null bila kosong/bentrok menunggu konfirmasi.
    public string? ExcelCode { get; set; }

    public bool Active { get; set; } = true;

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public ICollection<QcInspection> Inspections { get; set; } = new List<QcInspection>();

    public ICollection<QcFormApprovalStep> ApprovalSteps { get; set; } = new List<QcFormApprovalStep>();
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

    // Pemborong & mandor pelaksana (master QC lokal / sync).
    public int? PemborongId { get; set; }

    public QcPemborong? Pemborong { get; set; }

    public int? MandorId { get; set; }

    public QcMandor? Mandor { get; set; }

    // Inspektur pelaksana (user aplikasi); nama di-snapshot agar histori tetap terbaca.
    public int? InspectorUserId { get; set; }

    public UtlUser? Inspector { get; set; }

    public string? InspectorNameSnapshot { get; set; }

    // Nomor ICAR lama (format menunggu konfirmasi; disimpan teks bebas).
    public string? NoIcarLama { get; set; }

    public string Commodity { get; set; } = "";

    // Periode untuk reporting (opsional per form).
    public int? PeriodMonth { get; set; }

    public int? PeriodYear { get; set; }

    public string Status { get; set; } = QcInspectionStatus.Draft;

    // Level yang sedang menunggu approval (0 = belum submit).
    public int CurrentApprovalLevel { get; set; }

    // Siklus revisi: 0 = pengajuan pertama; +1 setiap resubmit setelah REVISION.
    // Baris approval lama per revisi tetap disimpan (unique InspectionId+Level+Revision).
    public int Revision { get; set; }

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
   3. QcApproval — one-to-many, satu row per (InspectionId, Level, Revision)
   ========================================================= */

public class QcApproval
{
    public long Id { get; set; }

    public long InspectionId { get; set; }

    public QcInspection? Inspection { get; set; }

    public int Level { get; set; }

    // Siklus revisi baris ini (mengikuti QcInspection.Revision saat dibuat).
    public int Revision { get; set; }

    public int? ApproverUserId { get; set; }

    public UtlUser? Approver { get; set; }

    public string? ApproverNameSnapshot { get; set; }

    public int? ApproverGroupId { get; set; }

    public UtlGroup? ApproverGroup { get; set; }

    // APPROVE / REQUEST_REVISION (kosong saat PENDING; REJECT hanya baris legacy).
    public string Action { get; set; } = "";

    // PENDING / APPROVED / REVISION_REQUIRED (REJECTED hanya baris legacy).
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

    // OPEN / IN_PROGRESS / DONE / VERIFIED
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

    // CREATE / UPDATE / SUBMIT / APPROVE / REQUEST_REVISION / RESUBMIT / CLOSE
    public string Action { get; set; } = "";

    public string? FromStatus { get; set; }

    public string? ToStatus { get; set; }

    public int? ApprovalLevel { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
