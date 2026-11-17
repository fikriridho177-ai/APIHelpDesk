using AuthUtility.Domain.Entities;

namespace AuthUtility.Api.Wad;

// Aturan bisnis laporan WAD yang murni (tanpa database) agar dapat diuji terpisah.
public static class WadReportRules
{
    public const int MaxPhotos = 5;
    public const long MaxPhotoBytes = 5 * 1024 * 1024;
    public const int MaxNoteLength = 2000;
    public const int MaxCaptionLength = 200;

    private const decimal FloodAreaTolerance = 0.0001m;

    public static decimal? PeatDrop(string? method, decimal? reference, decimal measured) => method switch
    {
        "ELEVATION" => reference - measured,
        "EXPOSED_ROD" => measured - reference,
        _ => null
    };

    public static string? PeatCondition(decimal? dropCm, decimal? verifyCm = null, decimal? criticalCm = null)
    {
        if (dropCm is null || verifyCm is null || criticalCm is null || verifyCm < 0 || criticalCm < verifyCm) return null;
        return dropCm >= criticalCm ? "KRITIS" : dropCm >= verifyCm ? "PERLU_VERIFIKASI" : "NORMAL";
    }

    public static int? FloodDurationDays(DateOnly observedDate, DateOnly? floodStartDate) =>
        floodStartDate is { } start && start <= observedDate
            ? observedDate.DayNumber - start.DayNumber
            : null;

    // Realisasi kumulatif / target kuantitas. Tanpa target, progres diisi manual.
    public static int? ProjectPercent(decimal? targetQty, decimal? realizationQty, int? manualPercent)
    {
        if (targetQty is > 0 && realizationQty is not null)
        {
            if (realizationQty <= 0) return 0;
            if (realizationQty >= targetQty) return 100;
            var percent = realizationQty.Value / targetQty.Value * 100m;
            return (int)Math.Round(percent, MidpointRounding.AwayFromZero);
        }

        return manualPercent is null ? null : Math.Clamp(manualPercent.Value, 0, 100);
    }

    public static string ProjectStatus(int percent, DateOnly? targetDate, DateOnly today)
    {
        if (percent >= 100) return "SELESAI";
        if (targetDate is { } target && today > target) return "TERLAMBAT";
        return "BERJALAN";
    }

    /* =========================================================
       VALIDASI INPUT
    ========================================================= */

    public static string? ValidateWater(WadReportInput input)
    {
        if (!WadReportCondition.Water.Contains(input.Condition ?? ""))
            return "Kondisi bangunan harus Baik, Perlu perbaikan, atau Rusak.";
        if (input.WaterLevelCm is < 0 or > 5000)
            return "Tinggi air harus antara 0 dan 5000 cm.";
        return ValidateCommon(input);
    }

    public static string? ValidatePeat(WadReportInput input, WadObject obj)
    {
        if (input.MeasurementValue is null)
            return "Nilai pengukuran wajib diisi.";
        var unit = (input.MeasurementUnit ?? "CM").Trim().ToUpperInvariant();
        if (unit is not ("CM" or "M"))
            return "Satuan pengukuran harus CM atau M.";
        var limit = unit == "M" ? 2000m : 200000m;
        if (input.MeasurementValue < -limit || input.MeasurementValue > limit)
            return "Nilai pengukuran di luar rentang wajar.";
        return ValidateCommon(input);
    }

    public static string? ValidateFlood(WadReportInput input, WadObject obj)
    {
        if (!WadReportCondition.Flood.Contains(input.Condition ?? ""))
            return "Kondisi genangan harus Surut, Berkurang, atau Meluas.";
        if (input.FloodAreaHa is null or < 0 || (input.FloodAreaHa == 0 && input.Condition != "SURUT"))
            return "Luas genangan harus positif, atau 0 ha untuk kondisi Surut.";
        if (input.FloodAreaHa > 99999999.9999m)
            return "Luas genangan melampaui kapasitas penyimpanan.";
        if (obj.AreaHa is > 0 && input.FloodAreaHa > obj.AreaHa.Value + FloodAreaTolerance)
            return $"Luas genangan tidak boleh melebihi luas persil {obj.AreaHa.Value:0.####} ha.";
        if (input.FloodHeightCm is < 0 or > 2000)
            return "Tinggi genangan harus antara 0 dan 2000 cm.";
        if (input.FloodStartDate is { } start && start > input.ObservedDate)
            return "Tanggal mulai genangan tidak boleh setelah tanggal pengamatan.";
        return ValidateCommon(input);
    }

    public static string? ValidateProject(WadReportInput input, WadObject obj)
    {
        var description = input.WorkDescription?.Trim() ?? "";
        if (description.Length > MaxNoteLength) return "Uraian pekerjaan maksimal 2000 karakter.";
        if (description.Length < 5)
            return "Uraian pekerjaan wajib diisi minimal 5 karakter.";
        if (input.RealizationQty is < 0 or > 99999999999.999m)
            return "Realisasi pekerjaan di luar rentang penyimpanan.";
        if (obj.TargetQty is > 0)
        {
            if (input.RealizationQty is null)
                return "Realisasi pekerjaan wajib diisi untuk proyek bertarget kuantitas.";
        }
        else if (input.ProgressPercent is null or < 0 or > 100)
        {
            return "Persentase progres wajib diisi antara 0 dan 100 untuk proyek tanpa target kuantitas.";
        }

        return ValidateCommon(input);
    }

    private static string? ValidateCommon(WadReportInput input)
    {
        if (input.Notes?.Length > MaxNoteLength)
            return $"Catatan maksimal {MaxNoteLength} karakter.";
        if (input.Constraints?.Length > MaxNoteLength)
            return $"Kendala maksimal {MaxNoteLength} karakter.";
        if (input.ObservedDate > DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)))
            return "Tanggal pengamatan tidak boleh di masa depan (WIB).";
        if (input.ObservedDate == default)
            return "Tanggal laporan wajib diisi.";
        return null;
    }
}
