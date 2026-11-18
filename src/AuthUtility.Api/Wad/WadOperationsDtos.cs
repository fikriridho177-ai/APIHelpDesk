namespace AuthUtility.Api.Wad;

public sealed record WadHubLoggerSummary(
    int Total,
    int Online,
    int Offline,
    int Internal,
    int Vendor,
    DateTime? LastReadingAt);

public sealed record WadHubAttentionItem(
    string KodeAset,
    string Wilayah,
    string Kanal,
    string NamaBangunan,
    string? KondisiFisik,
    string? TanggalLog,
    string Level);

public sealed record WadHubAttention(
    int Total,
    int OfflineLoggers,
    int DamagedBuildings,
    IReadOnlyList<WadHubAttentionItem> Items);

public sealed record WadHubServices(int Active, int Total);

public sealed record WadHubCoverage(IReadOnlyList<string> Wilayah, string? LatestWeek);

public sealed record WadHubFieldSummary(int PengamatanUnits, int SubsidenStakes);

public sealed record WadHubDevices(int Total, string? FirmwareVersion);

public sealed record WadHubSummaryDto(
    WadHubLoggerSummary Logger,
    WadHubAttention Attention,
    WadHubServices Services,
    WadHubCoverage Coverage,
    WadHubFieldSummary Field,
    WadHubDevices Devices,
    DateTime UpdatedAt);

public sealed record WadAirSensorValue(
    decimal? Nilai,
    string? Satuan,
    string? Lokasi,
    string? Tanggal);

public sealed record WadAirRainValue(
    decimal? CurahHujanMm,
    int? HariHujan,
    string? Tanggal,
    int PosTerbaca);

public sealed record WadAirItemDto(
    string KodeAset,
    string Wilayah,
    string Kanal,
    string NamaBangunan,
    string? UkuranDimensi,
    short? JumlahPapan,
    string JenisBangunan,
    string? StatusOperasi,
    string? DetailBukaan,
    string? KondisiFisik,
    bool PerluPerhatian,
    string? AlertLevel,
    string? TanggalLog,
    string? Minggu,
    WadAirSensorValue Tmak,
    WadAirSensorValue Tmat,
    WadAirRainValue CurahHujan,
    bool HasFoto,
    long? FotoId);

public sealed record WadAirListDto(
    int Total,
    string? LatestLogDate,
    IReadOnlyList<WadAirItemDto> Items,
    IReadOnlyList<string> Warnings);

public sealed record WadOverflowLogDto(
    long Id,
    string Minggu,
    string TanggalLog,
    string StatusOperasi,
    string? DetailBukaan,
    string? KondisiFisik,
    string? PetugasNama,
    string? PemeriksaNama,
    long? FotoId,
    DateTime CreatedAt);

public sealed record WadAirHistoryDto(
    string KodeAset,
    string Wilayah,
    string Kanal,
    string NamaBangunan,
    string? UkuranDimensi,
    short? JumlahPapan,
    IReadOnlyList<WadOverflowLogDto> Items);

public sealed record WadSubsidenPeriodDto(string Key, string Label);

public sealed record WadSubsidenValueDto(string Periode, decimal NilaiCm, decimal? DeltaCm, long? FotoId);

public sealed record WadSubsidenStakeDto(
    string KodePatok,
    string Wilayah,
    string Blok,
    string? LintangRaw,
    decimal? Lintang,
    string? BujurRaw,
    decimal? Bujur,
    int? TahunTanam,
    string? KedalamanGambut,
    decimal? LatestCm,
    IReadOnlyList<WadSubsidenValueDto> Values);

public sealed record WadSubsidenAverageDto(string Periode, string Label, decimal? NilaiCm);

public sealed record WadSubsidenResponse(
    IReadOnlyList<WadSubsidenPeriodDto> Periods,
    IReadOnlyList<WadSubsidenStakeDto> Stakes,
    IReadOnlyList<WadSubsidenAverageDto> Averages,
    string Source);

public sealed record WadSiponTemplateDto(
    IReadOnlyList<string> Fields,
    IReadOnlyList<string> KondisiOptions,
    IReadOnlyList<WadSiponChecklistItem> Checklist);

public sealed record WadSiponChecklistItem(int Nomor, string Label);

public sealed record WadSiponRecordDto(
    long Id,
    string Persil,
    string Tanggal,
    string Kondisi,
    decimal? TmaInletCm,
    decimal? TmaOutletCm,
    string? TanggulInlet,
    string? TanggulOutlet,
    string Komponen,
    string? Keterangan,
    string CreatedBy,
    DateTime CreatedAt);

public sealed record WadSiponResponse(
    WadSiponTemplateDto Template,
    IReadOnlyList<WadSiponRecordDto> Items);

public sealed record WadLahanTemplateDto(
    IReadOnlyList<string> Fields,
    IReadOnlyList<string> KondisiOptions,
    IReadOnlyList<string> AksesJalanOptions,
    IReadOnlyList<string> TanggulOptions);

public sealed record WadLahanRecordDto(
    long Id,
    string Persil,
    string Tanggal,
    string Kondisi,
    decimal? TmatCm,
    decimal? LuasHa,
    string? AksesJalan,
    string? Tanggul,
    string? Keterangan,
    string CreatedBy,
    DateTime CreatedAt);

public sealed record WadLahanResponse(
    WadLahanTemplateDto Template,
    IReadOnlyList<WadLahanRecordDto> Items);
