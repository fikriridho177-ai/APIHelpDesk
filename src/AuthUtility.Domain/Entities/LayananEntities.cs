namespace AuthUtility.Domain.Entities;

// Status pengajuan layanan (alur flowchart: Isi Formulir -> Diterima Admin -> Penjadwalan -> Proses -> Selesai).
public static class LynStatus
{
    public const string Baru = "BARU";
    public const string Diterima = "DITERIMA";
    public const string Dijadwalkan = "DIJADWALKAN";
    public const string Diproses = "DIPROSES";
    public const string MenungguInfo = "MENUNGGU_INFO";
    public const string Selesai = "SELESAI";
    public const string Ditolak = "DITOLAK";
    public const string Dibatalkan = "DIBATALKAN";

    public static readonly string[] Closed = { Selesai, Ditolak, Dibatalkan };

    // Tiket yang masih antri (belum mulai dikerjakan) dan mendapat nomor antrian.
    public static readonly string[] Waiting = { Baru, Diterima, Dijadwalkan };
}

// Master Jenis Permintaan (Pembuatan Peta, Drone Foto Udara, ...). Pembeda per divisi = DivisionMenuId
// (menu GROUP divisi di bawah LAYANAN, mis. LYN_GIS). Diatur di halaman Master Jenis Permintaan.
public class LynJenisPermintaan
{
    public int Id { get; set; }

    public int DivisionMenuId { get; set; }

    public UtlMenu? DivisionMenu { get; set; }

    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public string? Description { get; set; }

    // Jenis "Lainnya": pemohon menuliskan sendiri jenis permintaannya (disimpan di LynRequest.OtherType).
    public bool IsOther { get; set; }

    public int SortOrder { get; set; }

    public bool Active { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

// Skala prioritas mengikuti form permintaan: Normal, Sedang, Tinggi, Urgent.
public static class LynPriority
{
    public static readonly string[] All = { "NORMAL", "SEDANG", "TINGGI", "URGENT" };

    // Urutan antrian (kecil = didahulukan). RENDAH / MENDESAK = nilai lama sebelum 20 Okt 2026.
    public static int Rank(string? priority) => priority switch
    {
        "URGENT" or "MENDESAK" => 0,
        "TINGGI" => 1,
        "SEDANG" => 2,
        "NORMAL" or "RENDAH" => 3,
        _ => 4
    };
}

// Pengajuan layanan. MenuId = menu divisi (mis. LYN_GIS) untuk pengajuan dengan JenisId,
// atau sub-layanan (route /layanan/...) untuk pengajuan model lama tanpa jenis.
public class LynRequest
{
    public long Id { get; set; }

    public string? RequestNo { get; set; }

    public int MenuId { get; set; }

    public UtlMenu? Menu { get; set; }

    // Jenis Permintaan dari master LynJenisPermintaan.
    public int? JenisId { get; set; }

    public LynJenisPermintaan? Jenis { get; set; }

    public int RequesterUserId { get; set; }

    public UtlUser? Requester { get; set; }

    // Disalin dari UtlUser saat pengajuan dibuat (dipakai untuk filter Akses Departemen).
    public string? RequesterDeptId { get; set; }

    public int? RequesterCompanyId { get; set; }

    // Data pemohon seperti di form (diisi saat pengajuan dibuat, awalnya dari akun login).
    public string? RequesterName { get; set; }

    public string? RequesterPosition { get; set; }

    public string? RequesterDepartment { get; set; }

    // Perusahaan yang dipilih (boleh lebih dari satu), dipisah "; ".
    public string? RequesterCompanies { get; set; }

    public string? RequesterPhone { get; set; }

    public string? RequesterEmail { get; set; }

    // Isian "Yang lain" pada Jenis Permintaan (sub-layanan ..._LAINNYA).
    public string? OtherType { get; set; }

    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public string? Location { get; set; }

    public DateOnly? NeededDate { get; set; }

    public string Priority { get; set; } = "NORMAL";

    public string Status { get; set; } = LynStatus.Baru;

    public DateTime? ScheduledAt { get; set; }

    public string? Assignee { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public ICollection<LynRequestLog> Logs { get; set; } = new List<LynRequestLog>();
}

// Riwayat status & catatan pengajuan.
public class LynRequestLog
{
    public long Id { get; set; }

    public long RequestId { get; set; }

    public int UserId { get; set; }

    public UtlUser? User { get; set; }

    public string Action { get; set; } = "";

    public string? FromStatus { get; set; }

    public string? ToStatus { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
