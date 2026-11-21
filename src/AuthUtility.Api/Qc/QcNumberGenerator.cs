using Microsoft.Extensions.Configuration;

namespace AuthUtility.Api.Qc;

// Generator nomor inspeksi & nomor finding.
// Inspeksi (Fase 2): nomor atomik per scope form|wilayah|tahun|bulan dari
// MstQcNumberSeq, format configurable Qc:Numbering:InspectionPattern.
// Token: {formCode} {wilayah} {yyyy} {MM} {seq} (plus {prefix}/{year} legacy).
// Finding: tetap dari identity Id, pola Qc:Numbering:FindingPattern (QC-{prefix}-F-{year}-{seq}).
public class QcNumberGenerator(IConfiguration config, IQcNumberSeqStore seqStore)
{
    // Contoh hasil: QC-PANEN-KLP/W1/2026/11/0001
    public async Task<string> GenerateInspectionNoAsync(
        string formCode, string? wilayahKode, DateOnly inspectionDate, CancellationToken ct = default)
    {
        var pattern = config["Qc:Numbering:InspectionPattern"] ?? "{formCode}/{wilayah}/{yyyy}/{MM}/{seq}";
        var pad = SeqPad("InspectionSeqPad", 4);
        var wilayah = string.IsNullOrWhiteSpace(wilayahKode) ? "-" : wilayahKode.Trim();
        var yyyy = inspectionDate.Year.ToString("D4");
        var mm = inspectionDate.Month.ToString("D2");

        var scopeKey = $"{formCode}|{wilayah}|{yyyy}|{mm}";
        var seq = await seqStore.NextAsync(scopeKey, ct);

        return pattern
            .Replace("{formCode}", formCode)
            .Replace("{wilayah}", wilayah)
            .Replace("{yyyy}", yyyy)
            .Replace("{MM}", mm)
            .Replace("{seq}", seq.ToString().PadLeft(pad, '0'));
    }

    // Contoh hasil: QC-KLP-F-2027-000001
    public string GenerateFindingNo(string commodity, long id)
    {
        var pattern = config["Qc:Numbering:FindingPattern"] ?? "QC-{prefix}-F-{year}-{seq}";
        var pad = SeqPad("SeqPad", 6);
        var prefix = GetPrefix(commodity);
        var year = DateTime.UtcNow.AddHours(7).Year.ToString(); // WIB (UTC+7)

        return pattern
            .Replace("{prefix}", prefix)
            .Replace("{year}", year)
            .Replace("{seq}", id.ToString().PadLeft(pad, '0'));
    }

    private int SeqPad(string key, int fallback) =>
        int.TryParse(config[$"Qc:Numbering:{key}"], out var p) ? Math.Clamp(p, 1, 12) : fallback;

    private string GetPrefix(string commodity)
    {
        var p = config[$"Qc:Numbering:PrefixByCommodity:{commodity}"];
        return string.IsNullOrWhiteSpace(p) ? commodity : p;
    }
}