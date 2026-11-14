using Microsoft.Extensions.Configuration;

namespace AuthUtility.Api.Qc;

// Generator nomor inspeksi & nomor finding. Format dari config (Qc:Numbering),
// dipakai sesudah insert (dari identity Id) mengikuti pola LYN-{yyMMdd}-{Id:D6}.
// Token: {prefix} {year} {seq}  — mudah diubah tanpa kode.
public class QcNumberGenerator
{
    private readonly IConfiguration _config;

    public QcNumberGenerator(IConfiguration config) => _config = config;

    // Contoh hasil: QC-KLP-2027-000001
    public string GenerateInspectionNo(string commodity, long id) =>
        Build("InspectionPattern", commodity, id);

    // Contoh hasil: QC-KLP-F-2027-000001
    public string GenerateFindingNo(string commodity, long id) =>
        Build("FindingPattern", commodity, id);

    private string Build(string patternKey, string commodity, long id)
    {
        var pattern = _config[$"Qc:Numbering:{patternKey}"]
            ?? (patternKey == "FindingPattern"
                ? "QC-{prefix}-F-{year}-{seq}"
                : "QC-{prefix}-{year}-{seq}");

        var pad = int.TryParse(_config["Qc:Numbering:SeqPad"], out var p)
            ? Math.Clamp(p, 1, 12)
            : 6;

        var prefix = GetPrefix(commodity);
        var year = DateTime.UtcNow.AddHours(7).Year.ToString(); // WIB (UTC+7)

        return pattern
            .Replace("{prefix}", prefix)
            .Replace("{year}", year)
            .Replace("{seq}", id.ToString().PadLeft(pad, '0'));
    }

    private string GetPrefix(string commodity)
    {
        var p = _config[$"Qc:Numbering:PrefixByCommodity:{commodity}"];
        return string.IsNullOrWhiteSpace(p) ? commodity : p;
    }
}
