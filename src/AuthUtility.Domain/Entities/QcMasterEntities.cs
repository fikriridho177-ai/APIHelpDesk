namespace AuthUtility.Domain.Entities;

/* =========================================================
   Master QC Fase 1 — fondasi & master.
   Cache persil di-sync read-only dari SQL Server lewat PersilReader;
   master lain CRUD lokal. Soft delete lewat kolom Active.
   ========================================================= */

// Alur approval per form (sumber kebenaran menggantikan config appsettings pada Fase 2).
public class QcFormApprovalStep
{
    public int Id { get; set; }

    public int FormId { get; set; }

    public QcForm? Form { get; set; }

    public int Level { get; set; }

    public string Label { get; set; } = "";

    // Kode jabatan untuk label cetak & pencocokan approver bila group belum dipetakan.
    public string? JabatanKode { get; set; }

    public int? ApproverGroupId { get; set; }

    public UtlGroup? ApproverGroup { get; set; }

    // Menu hak approve (dipakai QcAccess.CanApproveStep).
    public string MenuCode { get; set; } = "QC_APPROVAL";

    // Approver harus sesuai afdeling/wilayah persil inspeksi.
    public bool ScopeByAfdeling { get; set; }

    // Baris pembuat dokumen (level pertama): otomatis APPROVED atas nama
    // pembuat saat submit/resubmit, bukan aksi approver manual.
    public bool IsCreatorStep { get; set; }

    public bool Active { get; set; } = true;

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

// Cache master persil dari SQL Server (KLP / TNM_NNS / LHN_NNS / PNN). Angka mentah.
public class QcPersil
{
    public int Id { get; set; }

    public string Sumber { get; set; } = "";

    public string PersilId { get; set; } = "";

    public string PersilKode { get; set; } = "";

    public string? NamaPersil { get; set; }

    public string Komoditas { get; set; } = "";

    public string? WilayahKode { get; set; }

    public string? KodeDivisi { get; set; }

    public string? KodeAfdeling { get; set; }

    public string? Kcb { get; set; }

    public int? BulanTanam { get; set; }

    public int? TahunTanam { get; set; }

    public decimal? LuasHa { get; set; }

    public int? JumlahJalur { get; set; }

    public int? JumlahPohon { get; set; }

    public string? PemborongKode { get; set; }

    public string? PemborongNama { get; set; }

    public bool Active { get; set; } = true;

    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

public class QcPemborong
{
    public int Id { get; set; }

    public string Kode { get; set; } = "";

    public string Name { get; set; } = "";

    public string? WilayahKode { get; set; }

    // LOCAL = diisi user; SYNC = ditemukan di sumber persil.
    public string Sumber { get; set; } = "LOCAL";

    public bool Active { get; set; } = true;

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class QcMandor
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string? WilayahKode { get; set; }

    public string? KodeAfdeling { get; set; }

    public int? PemborongId { get; set; }

    public QcPemborong? Pemborong { get; set; }

    public bool Active { get; set; } = true;

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class QcJenisPupuk
{
    public int Id { get; set; }

    public string Kode { get; set; } = "";

    public string Name { get; set; } = "";

    public string Satuan { get; set; } = "gram";

    public decimal? DosisJadwalGram { get; set; }

    public string? Komoditas { get; set; }

    public bool Active { get; set; } = true;

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class QcGrade
{
    public int Id { get; set; }

    public string Kode { get; set; } = "";

    public string Name { get; set; } = "";

    public string? Komoditas { get; set; }

    public int Urutan { get; set; }

    public bool Active { get; set; } = true;

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

// Target % per form per parameter: dasar validasi & tombol "Buat temuan".
public class QcStandard
{
    public int Id { get; set; }

    public int FormId { get; set; }

    public QcForm? Form { get; set; }

    public string ParameterKey { get; set; } = "";

    public decimal TargetPercent { get; set; }

    // MAX = nilai tidak boleh melebihi target; MIN = tidak boleh kurang.
    public string Operator { get; set; } = "MAX";

    public string? Keterangan { get; set; }

    public bool Active { get; set; } = true;

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

// Penomoran atomik per scope (dipakai Fase 2): INSERT ... ON CONFLICT DO UPDATE ... RETURNING.
public class QcNumberSeq
{
    public int Id { get; set; }

    public string ScopeKey { get; set; } = "";

    public int LastValue { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}