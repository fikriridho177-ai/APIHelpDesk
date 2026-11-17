namespace AuthUtility.Api.Wad;

// Payload create/update laporan. Field yang tidak dipakai modul dibiarkan null.
public sealed class WadReportInput
{
    public Guid RequestId { get; set; }

    public int ObjectId { get; set; }

    public DateOnly ObservedDate { get; set; }

    public TimeOnly? ObservedTime { get; set; }

    public string? Condition { get; set; }

    public decimal? WaterLevelCm { get; set; }

    public decimal? MeasurementValue { get; set; }

    public string? MeasurementUnit { get; set; }

    public decimal? FloodAreaHa { get; set; }

    public decimal? FloodHeightCm { get; set; }

    public DateOnly? FloodStartDate { get; set; }

    public string? WorkDescription { get; set; }

    public decimal? RealizationQty { get; set; }

    public int? ProgressPercent { get; set; }

    public string? Constraints { get; set; }

    public string? Notes { get; set; }
}
