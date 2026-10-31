namespace AuthUtility.Domain.Entities;

// Sensus diisi Asisten Lapangan dari Android, di web hanya approval, monitoring, analisis & master.
// Lihat POSTGRES_SENSUS_MIGRATION.sql
public static class SnsStatus
{
    public const string Submitted = "SUBMITTED";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Revisi = "REVISI";

    public static readonly string[] All = { Submitted, Approved, Rejected, Revisi };
}

public static class SnsKomoditas
{
    public static readonly string[] Blok = { "KELAPA", "NANAS" };
    public static readonly string[] Jenis = { "KELAPA", "NANAS", "SEMUA" };
}

public static class SnsTipeData
{
    public static readonly string[] All = { "NUMBER", "TEXT", "BOOLEAN", "OPTION" };
}

/* ---------- Master lokasi ---------- */

public class SnsKebun
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int? CompanyId { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class SnsAfdeling
{
    public int Id { get; set; }
    public int KebunId { get; set; }
    public SnsKebun? Kebun { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class SnsBlok
{
    public int Id { get; set; }
    public int AfdelingId { get; set; }
    public SnsAfdeling? Afdeling { get; set; }
    public string Code { get; set; } = "";
    public string? Name { get; set; }
    public string Komoditas { get; set; } = "KELAPA";
    public decimal? LuasHa { get; set; }
    public int? TahunTanam { get; set; }
    public int? JumlahPokok { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

/* ---------- Master jenis sensus ---------- */

public class SnsJenis
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kategori { get; set; } = "HPT";
    public string Komoditas { get; set; } = "KELAPA";
    public string? MenuCode { get; set; }
    public string? Description { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public ICollection<SnsParameter> Parameters { get; set; } = new List<SnsParameter>();
}

public class SnsParameter
{
    public int Id { get; set; }
    public int JenisId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Satuan { get; set; }
    public string TipeData { get; set; } = "NUMBER";
    public string? Opsi { get; set; }
    public bool Wajib { get; set; }
    public bool IsIndikator { get; set; }
    public decimal? AmbangSedang { get; set; }
    public decimal? AmbangBerat { get; set; }
    public bool Active { get; set; } = true;
}

/* ---------- Transaksi sensus ---------- */

public class SnsHeader
{
    public long Id { get; set; }
    public string? SensusNo { get; set; }
    public Guid ClientUuid { get; set; }
    public int JenisId { get; set; }
    public SnsJenis? Jenis { get; set; }
    // Lokasi = persil dari MyPAD / RSUPPKB (lihat PersilReader). Kode, wilayah & afdeling disalin saat sensus dikirim
    // supaya daftar / analisis tidak perlu membaca database lain.
    public string PersilSumber { get; set; } = "";
    public string PersilId { get; set; } = "";
    public string PersilKode { get; set; } = "";
    public string? KodeWilayah { get; set; }
    public string? KodeAfdeling { get; set; }
    public string Komoditas { get; set; } = "KELAPA";
    // Kolom lama (master blok lokal), tidak dipakai lagi untuk sensus baru.
    public int? BlokId { get; set; }
    public SnsBlok? Blok { get; set; }
    public DateOnly TanggalSensus { get; set; }
    public int PetugasUserId { get; set; }
    public UtlUser? Petugas { get; set; }
    public int JumlahPokokSampel { get; set; }
    public int JumlahPokokTerserang { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Catatan { get; set; }
    public string? DeviceInfo { get; set; }
    public string Status { get; set; } = SnsStatus.Submitted;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public int? ApprovedByUserId { get; set; }
    public UtlUser? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovalNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public ICollection<SnsDetail> Details { get; set; } = new List<SnsDetail>();
    public ICollection<SnsFoto> Fotos { get; set; } = new List<SnsFoto>();
}

public class SnsDetail
{
    public long Id { get; set; }
    public long HeaderId { get; set; }
    public int ParameterId { get; set; }
    public SnsParameter? Parameter { get; set; }
    public int? NoPokok { get; set; }
    public decimal? NilaiAngka { get; set; }
    public string? NilaiTeks { get; set; }
}

public class SnsFoto
{
    public long Id { get; set; }
    public long HeaderId { get; set; }
    public string FileUrl { get; set; } = "";
    public string? Keterangan { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime? TakenAt { get; set; }
}

public class SnsLog
{
    public long Id { get; set; }
    public long HeaderId { get; set; }
    public int UserId { get; set; }
    public UtlUser? User { get; set; }
    public string Action { get; set; } = "";
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
