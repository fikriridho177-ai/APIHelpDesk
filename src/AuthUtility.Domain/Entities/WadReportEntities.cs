namespace AuthUtility.Domain.Entities;

// Modul laporan WAD. Disimpan sebagai kode stabil, label tampilan ada di frontend.
public static class WadReportModule
{
    public const string Water = "WATER";
    public const string Peat = "PEAT";
    public const string Flood = "FLOOD";
    public const string Project = "PROJECT";

    public static readonly string[] All = { Water, Peat, Flood, Project };

    public static bool IsValid(string? module) => module != null && All.Contains(module);
}

public static class WadReportCondition
{
    public static readonly string[] Water = { "BAIK", "PERLU_PERBAIKAN", "RUSAK" };
    public static readonly string[] Flood = { "SURUT", "BERKURANG", "MELUAS" };
    public static readonly string[] Peat = { "NORMAL", "PERLU_VERIFIKASI", "KRITIS" };
}

public static class WadPhotoPhase
{
    public const string Before = "SEBELUM";
    public const string During = "PROSES";
    public const string After = "SESUDAH";

    public static readonly string[] All = { Before, During, After };
}

// Objek/lokasi master: bangunan air, patok gambut, persil, atau projek.
public class WadObject
{
    public int Id { get; set; }

    public string Module { get; set; } = "";

    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public string ObjectType { get; set; } = "";

    public string Wilayah { get; set; } = "";

    public string? Location { get; set; }

    public bool IsDemo { get; set; }

    public string? MeasurementMethod { get; set; }

    public decimal? VerifyDropCm { get; set; }

    public decimal? CriticalDropCm { get; set; }

    // Acuan elevasi patok dalam cm.
    public decimal? ReferenceValueCm { get; set; }

    // Luas persil (bukan luas genangan) dalam hektar.
    public decimal? AreaHa { get; set; }

    public string? PicName { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? TargetDate { get; set; }

    // Target kuantitas pekerjaan; null berarti progres diisi persen manual.
    public decimal? TargetQty { get; set; }

    public string? Unit { get; set; }

    public bool Active { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class WadReport
{
    public long Id { get; set; }

    public int ObjectId { get; set; }

    public Guid? RequestId { get; set; }

    public string? MeasurementMethod { get; set; }

    public string Module { get; set; } = "";

    public DateOnly ObservedDate { get; set; }

    public TimeOnly? ObservedTime { get; set; }

    public string? Condition { get; set; }

    public decimal? WaterLevelCm { get; set; }

    // Nilai terukur patok sebagaimana diisi, beserta satuannya.
    public decimal? MeasurementValue { get; set; }

    public string? MeasurementUnit { get; set; }

    // Nilai terukur yang sudah dinormalkan ke cm.
    public decimal? MeasuredValueCm { get; set; }

    // Snapshot acuan patok saat laporan dibuat.
    public decimal? ReferenceValueCm { get; set; }

    // Penurunan = acuan - terukur (positif berarti turun), dalam cm.
    public decimal? DropCm { get; set; }

    public decimal? FloodAreaHa { get; set; }

    public decimal? FloodHeightCm { get; set; }

    public DateOnly? FloodStartDate { get; set; }

    public int? FloodDurationDays { get; set; }

    public string? WorkDescription { get; set; }

    public decimal? RealizationQty { get; set; }

    public string? RealizationUnit { get; set; }

    public int? ProgressPercent { get; set; }

    public string? Constraints { get; set; }

    public string? Notes { get; set; }

    public int CreatedByUserId { get; set; }

    public string CreatedByName { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedByUserId { get; set; }

    public WadObject? Object { get; set; }

    public ICollection<WadReportPhoto> Photos { get; set; } = new List<WadReportPhoto>();
}

public class WadReportPhoto
{
    public long Id { get; set; }

    public long ReportId { get; set; }

    public Guid? RequestId { get; set; }

    public string FileUrl { get; set; } = "";

    public string? Keterangan { get; set; }

    public string? Phase { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public WadReport? Report { get; set; }
}
