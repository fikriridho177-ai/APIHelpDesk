namespace AuthUtility.Application.DTOs.Qc;

// DTO master QC Fase 1. Label & pesan error berbahasa Indonesia.

public record QcFormMasterDto(
    int Id, string Code, string Name, string Commodity, string? Description, bool Active,
    int Version, DateOnly? EffectiveFrom, string? Pattern, string? TemplateFile,
    string? PrintTitle, string? ExcelCode, int InspectionCount, DateTime CreatedAt, DateTime? UpdatedAt);

public record QcFormSaveDto(
    string Code, string Name, string Commodity, string? Description,
    string? Pattern, string? TemplateFile, string? PrintTitle, string? ExcelCode, DateOnly? EffectiveFrom);

public record QcApprovalStepDto(
    int Id, int FormId, int Level, string Label, string? JabatanKode,
    int? ApproverGroupId, string? ApproverGroupName, string MenuCode, bool ScopeByAfdeling, bool IsCreatorStep, bool Active);

public record QcApprovalStepSaveDto(
    int FormId, int Level, string Label, string? JabatanKode,
    int? ApproverGroupId, string? MenuCode, bool ScopeByAfdeling, bool IsCreatorStep = false);

public record QcPersilDto(
    int Id, string Sumber, string PersilId, string PersilKode, string? NamaPersil, string Komoditas,
    string? WilayahKode, string? KodeDivisi, string? KodeAfdeling, string? Kcb,
    int? BulanTanam, int? TahunTanam, decimal? LuasHa, int? JumlahJalur, int? JumlahPohon,
    string? PemborongKode, string? PemborongNama, bool Active, DateTime SyncedAt);

public record QcPersilPageDto(
    IReadOnlyList<QcPersilDto> Rows, int Total, int Page, int PageSize,
    DateTime? LastSyncedAt, IReadOnlyList<QcPersilSumberCount> PerSumber);

public record QcPersilSumberCount(string Sumber, int Total, int Aktif);

public record QcPemborongDto(int Id, string Kode, string Name, string? WilayahKode, string Sumber, bool Active);

public record QcPemborongSaveDto(string Kode, string Name, string? WilayahKode);

public record QcMandorDto(int Id, string Name, string? WilayahKode, string? KodeAfdeling, int? PemborongId, string? PemborongName, bool Active);

public record QcMandorSaveDto(string Name, string? WilayahKode, string? KodeAfdeling, int? PemborongId);

public record QcJenisPupukDto(int Id, string Kode, string Name, string Satuan, decimal? DosisJadwalGram, string? Komoditas, bool Active);

public record QcJenisPupukSaveDto(string Kode, string Name, string? Satuan, decimal? DosisJadwalGram, string? Komoditas);

public record QcGradeDto(int Id, string Kode, string Name, string? Komoditas, int Urutan, bool Active);

public record QcGradeSaveDto(string Kode, string Name, string? Komoditas, int Urutan);

public record QcStandardDto(int Id, int FormId, string FormCode, string ParameterKey, decimal TargetPercent, string Operator, string? Keterangan, bool Active);

public record QcStandardSaveDto(int FormId, string ParameterKey, decimal TargetPercent, string? Operator, string? Keterangan);

public record QcActiveDto(bool Active);