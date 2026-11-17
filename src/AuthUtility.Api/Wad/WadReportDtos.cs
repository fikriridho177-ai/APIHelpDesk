namespace AuthUtility.Api.Wad;

public sealed record WadPhotoDto(long Id, string? Keterangan, string? Phase, int SortOrder, string Url);

public sealed record WadObjectDto(
    int Id,
    string Module,
    string Code,
    string Name,
    string ObjectType,
    string Wilayah,
    string? Location,
    decimal? ReferenceValueCm,
    decimal? AreaHa,
    string? PicName,
    DateOnly? StartDate,
    DateOnly? TargetDate,
    decimal? TargetQty,
    string? Unit,
    long? LatestReportId,
    DateOnly? LatestObservedDate,
    string? LatestCondition,
    decimal? LatestDropCm,
    int? LatestProgressPercent,
    long? LatestPhotoId,
    bool IsDemo,
    string? MeasurementMethod,
    decimal? VerifyDropCm,
    decimal? CriticalDropCm);

public sealed record WadReportDto(
    long Id,
    string Module,
    int ObjectId,
    string ObjectCode,
    string ObjectName,
    string ObjectType,
    string Wilayah,
    string? Location,
    DateOnly ObservedDate,
    TimeOnly? ObservedTime,
    string? Condition,
    decimal? WaterLevelCm,
    decimal? MeasurementValue,
    string? MeasurementUnit,
    decimal? MeasuredValueCm,
    decimal? ReferenceValueCm,
    decimal? DropCm,
    decimal? FloodAreaHa,
    decimal? FloodHeightCm,
    DateOnly? FloodStartDate,
    int? FloodDurationDays,
    string? WorkDescription,
    decimal? RealizationQty,
    string? RealizationUnit,
    int? ProgressPercent,
    string? Constraints,
    string? Notes,
    int CreatedByUserId,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<WadPhotoDto> Photos,
    bool IsDemo,
    string? MeasurementMethod)
{
    public bool CanEdit { get; init; }
}

public sealed record WadReportListDto(int Total, int Page, int PageSize, IReadOnlyList<WadReportDto> Items);

public sealed record WadWaterSummary(
    int Buildings,
    int Gates,
    int Siphons,
    int Culverts,
    int ObservedBuildings,
    int NormalCondition,
    int NeedsRepair,
    int Damaged);

public sealed record WadPeatSummary(
    int Stakes,
    int Measurements,
    int ObservedStakes,
    decimal? AverageDropCm,
    decimal? MaxDropCm,
    int CriticalStakes);

public sealed record WadFloodSummary(
    int Parcels,
    int FloodedParcels,
    decimal TotalFloodAreaHa,
    int OverSevenDays);

public sealed record WadProjectSummary(
    int Projects,
    int Completed,
    int Running,
    int Late,
    int AverageProgress);

public sealed record WadSummaryDto(
    string Module,
    int TotalReports,
    int UniqueObjects,
    DateOnly? LatestObservedDate,
    WadWaterSummary? Water,
    WadPeatSummary? Peat,
    WadFloodSummary? Flood,
    WadProjectSummary? Project);
