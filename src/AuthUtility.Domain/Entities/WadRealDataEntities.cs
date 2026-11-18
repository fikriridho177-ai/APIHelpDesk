namespace AuthUtility.Domain.Entities;

// Data operasional WAD hasil impor paket "1. Helpdesk ASD".
// Skema: sql/2026-11-17-wad-real-data.sql.

public class WadBuilding
{
    public long Id { get; set; }
    public string KodeAset { get; set; } = "";
    public string Wilayah { get; set; } = "";
    public string Kanal { get; set; } = "";
    public string NamaBangunan { get; set; } = "";
    public string? UkuranDimensi { get; set; }
    public short? JumlahPapan { get; set; }
    public string JenisBangunan { get; set; } = "OVERFLOW";
    public string? RefTmakLokasi { get; set; }
    public string? RefTpmatLokasi { get; set; }
    public bool Aktif { get; set; } = true;
    public string? KondisiTerakhir { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public ICollection<WadOverflowLog> Logs { get; set; } = new List<WadOverflowLog>();
}

public class WadOverflowLog
{
    public long Id { get; set; }
    public string KodeAset { get; set; } = "";
    public string Minggu { get; set; } = "";
    public DateOnly TanggalLog { get; set; }
    public string StatusOperasi { get; set; } = "";
    public string? DetailBukaan { get; set; }
    public string? KondisiFisik { get; set; }
    public string? PetugasNama { get; set; }
    public string? PemeriksaNama { get; set; }
    public Guid RequestId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public WadBuilding? Building { get; set; }
    public ICollection<WadOverflowLogPhoto> Photos { get; set; } = new List<WadOverflowLogPhoto>();
}

public class WadOverflowLogPhoto
{
    public long Id { get; set; }
    public long LogId { get; set; }
    public string FileUrl { get; set; } = "";
    public string? Keterangan { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public WadOverflowLog? Log { get; set; }
}

public class WadSubsidenObservation
{
    public long Id { get; set; }
    public string KodePatok { get; set; } = "";
    public string? LintangRaw { get; set; }
    public decimal? Lintang { get; set; }
    public string? BujurRaw { get; set; }
    public decimal? Bujur { get; set; }
    public string Wilayah { get; set; } = "";
    public string Blok { get; set; } = "";
    public int? TahunTanam { get; set; }
    public string? KedalamanGambut { get; set; }
    public string Periode { get; set; } = "";
    public string PeriodeLabel { get; set; } = "";
    public decimal NilaiCm { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class WadSubsidenPhoto
{
    public long Id { get; set; }
    public string KodePatok { get; set; } = "";
    public string Periode { get; set; } = "";
    public string FileUrl { get; set; } = "";
    public string? Keterangan { get; set; }
    public short SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class WadSiponInspection
{
    public long Id { get; set; }
    public string Persil { get; set; } = "";
    public decimal? Lintang { get; set; }
    public decimal? Bujur { get; set; }
    public DateOnly Tanggal { get; set; }
    public string Kondisi { get; set; } = "";
    public decimal? TmaInletCm { get; set; }
    public decimal? TmaOutletCm { get; set; }
    public string? TanggulInlet { get; set; }
    public string? TanggulOutlet { get; set; }
    public string Komponen { get; set; } = "[]";
    public string? Keterangan { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class WadFloodedLandObservation
{
    public long Id { get; set; }
    public string Persil { get; set; } = "";
    public decimal? Lintang { get; set; }
    public decimal? Bujur { get; set; }
    public DateOnly Tanggal { get; set; }
    public string Kondisi { get; set; } = "";
    public decimal? TmatCm { get; set; }
    public decimal? LuasHa { get; set; }
    public string? AksesJalan { get; set; }
    public string? Tanggul { get; set; }
    public string? Keterangan { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
