namespace AuthUtility.Application.DTOs.Sensus;

/* =========================================================
   MASTER
========================================================= */

public record SnsParameterDto(
    int Id,
    int JenisId,
    string Code,
    string Name,
    string? Satuan,
    string TipeData,
    string? Opsi,
    bool Wajib,
    bool IsIndikator,
    decimal? AmbangSedang,
    decimal? AmbangBerat,
    bool Active
);

public record SnsJenisDto(
    int Id,
    string Code,
    string Name,
    string Kategori,
    string Komoditas,
    string? MenuCode,
    string? Description,
    bool Active,
    List<SnsParameterDto> Parameters
);

// Jenis sensus untuk halaman sub-layanan RNA + hak user di halaman tersebut.
public record SnsJenisPageDto(SnsJenisDto Jenis, bool CanRead, bool CanApprove, bool CanExport);

public class SaveSnsJenisRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kategori { get; set; } = "HPT";
    public string Komoditas { get; set; } = "KELAPA";
    public string? MenuCode { get; set; }
    public string? Description { get; set; }
    public bool Active { get; set; } = true;
}

public class SaveSnsParameterRequest
{
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

/* =========================================================
   SENSUS (APPROVAL & MONITORING)
========================================================= */

public record SnsListItemDto(
    long Id,
    string? SensusNo,
    int JenisId,
    string JenisName,
    string Kategori,
    string PersilSumber,
    string PersilId,
    string PersilKode,
    string? KodeWilayah,
    string? KodeAfdeling,
    string Komoditas,
    DateOnly TanggalSensus,
    int PetugasUserId,
    string PetugasName,
    int JumlahPokokSampel,
    int JumlahPokokTerserang,
    string Status,
    string Tingkat,
    DateTime SubmittedAt,
    DateTime? ApprovedAt
);

public record SnsDetailValueDto(
    long Id,
    int ParameterId,
    string ParameterName,
    string? Satuan,
    string TipeData,
    bool IsIndikator,
    int? NoPokok,
    decimal? NilaiAngka,
    string? NilaiTeks
);

// Rata-rata per parameter (untuk sensus per pokok sampel).
public record SnsParameterSummaryDto(
    int ParameterId,
    string ParameterName,
    string? Satuan,
    bool IsIndikator,
    decimal? Rata2,
    decimal? Total,
    int JumlahData,
    string Tingkat
);

public record SnsFotoDto(long Id, string FileUrl, string? Keterangan, decimal? Latitude, decimal? Longitude, DateTime? TakenAt);

public record SnsLogDto(long Id, string Action, string? FromStatus, string? ToStatus, string? Note, string UserName, DateTime CreatedAt);

public record SnsDetailDto(
    SnsListItemDto Summary,
    decimal? Latitude,
    decimal? Longitude,
    string? Catatan,
    string? DeviceInfo,
    string? ApprovedByName,
    string? ApprovalNote,
    bool CanApprove,
    List<SnsParameterSummaryDto> Ringkasan,
    List<SnsDetailValueDto> Details,
    List<SnsFotoDto> Fotos,
    List<SnsLogDto> Logs
);

public class SnsActionRequest
{
    // APPROVE, REJECT, REVISI
    public string Action { get; set; } = "";
    public string? Note { get; set; }
}

public class SnsBulkApproveRequest
{
    public List<long> Ids { get; set; } = new();
    public string? Note { get; set; }
}

public record SnsCountDto(string Key, string Label, int Total, int Submitted, int Approved, int Rejected, int Revisi);

public record SnsPersilTerlambatDto(
    string PersilSumber,
    string PersilId,
    string PersilKode,
    string? KodeWilayah,
    string? KodeAfdeling,
    int JenisId,
    string JenisName,
    DateOnly? TerakhirSensus,
    int? HariSejak
);

public record SnsMonitoringDto(
    int Total,
    int Submitted,
    int Approved,
    int Rejected,
    int Revisi,
    List<SnsCountDto> PerJenis,
    List<SnsCountDto> PerPetugas,
    List<SnsCountDto> PerWilayah,
    List<SnsCountDto> PerHari,
    // Sumber persil yang dipakai menghitung "terlambat" (KLP, TNM_NNS, LHN_NNS, PNN).
    string SumberTerlambat,
    List<SnsPersilTerlambatDto> PersilTerlambat,
    string? PersilTerlambatError
);

/* =========================================================
   ANALISIS (hanya data APPROVED)
========================================================= */

public record SnsAnalisisPersilDto(
    string PersilSumber,
    string PersilId,
    string PersilKode,
    string? KodeWilayah,
    string? KodeAfdeling,
    int JumlahSensus,
    DateOnly TerakhirSensus,
    int TotalPokokSampel,
    int TotalPokokTerserang,
    decimal PersenTerserang,
    decimal? RataIndikator,
    string Tingkat
);

public record SnsTrenDto(string Periode, int JumlahSensus, decimal PersenTerserang, decimal? RataIndikator);

public record SnsAnalisisDto(
    int JenisId,
    string JenisName,
    string? IndikatorName,
    string? IndikatorSatuan,
    decimal? AmbangSedang,
    decimal? AmbangBerat,
    int JumlahSensus,
    int JumlahPersil,
    decimal PersenTerserang,
    Dictionary<string, int> SebaranTingkat,
    List<SnsAnalisisPersilDto> PerPersil,
    List<SnsTrenDto> Tren
);

/* =========================================================
   ANDROID (Asisten Lapangan)
========================================================= */

public class SnsMobileDetailRequest
{
    public int? ParameterId { get; set; }
    public string? ParameterCode { get; set; }
    public int? NoPokok { get; set; }
    public decimal? NilaiAngka { get; set; }
    public string? NilaiTeks { get; set; }
}

public class SnsMobileFotoRequest
{
    public string FileUrl { get; set; } = "";
    public string? Keterangan { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime? TakenAt { get; set; }
}

public class SnsMobileSubmitRequest
{
    public Guid ClientUuid { get; set; }
    public int? JenisId { get; set; }
    public string? JenisCode { get; set; }
    // Persil dari GET /api/sensus/mobile/persil: sumber (KLP, TNM_NNS, LHN_NNS, PNN) + PersilID.
    public string PersilSumber { get; set; } = "";
    public string PersilId { get; set; } = "";
    public DateOnly TanggalSensus { get; set; }
    public int JumlahPokokSampel { get; set; }
    public int JumlahPokokTerserang { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Catatan { get; set; }
    public string? DeviceInfo { get; set; }
    public List<SnsMobileDetailRequest> Details { get; set; } = new();
    public List<SnsMobileFotoRequest> Fotos { get; set; } = new();
}

public record SnsMobileSubmitResponse(long Id, string? SensusNo, Guid ClientUuid, string Status, bool Duplicate, string Message);

public record SnsPersilSumberDto(string Key, string Label, string Database, string Komoditas);

public record SnsMobileMasterDto(List<SnsJenisDto> Jenis, List<SnsPersilSumberDto> SumberPersil);
