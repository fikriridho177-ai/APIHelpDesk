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
}

public static class LynPriority
{
    public static readonly string[] All = { "RENDAH", "NORMAL", "TINGGI", "MENDESAK" };
}

// Pengajuan layanan. MenuId = sub-layanan (baris UtlMenu dengan route /layanan/...).
public class LynRequest
{
    public long Id { get; set; }

    public string? RequestNo { get; set; }

    public int MenuId { get; set; }

    public UtlMenu? Menu { get; set; }

    public int RequesterUserId { get; set; }

    public UtlUser? Requester { get; set; }

    // Disalin dari UtlUser saat pengajuan dibuat (dipakai untuk filter Akses Departemen).
    public string? RequesterDeptId { get; set; }

    public int? RequesterCompanyId { get; set; }

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
