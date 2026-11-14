namespace AuthUtility.Api.Qc;

public sealed class QcApprovalFlowConfig(IConfiguration config)
{
    public sealed record ApprovalLevel(int Level, string MenuCode, string? GroupCode, string Label, string? RoleName);

    public IReadOnlyList<ApprovalLevel> GetLevels(string formCode)
    {
        var section = config.GetSection($"Qc:ApprovalFlows:{formCode}:Levels");
        var levels = new List<ApprovalLevel>();

        if (section.Exists())
        {
            foreach (var child in section.GetChildren())
            {
                if (!int.TryParse(child["Level"], out var level) || level <= 0) continue;
                var menu = string.IsNullOrWhiteSpace(child["MenuCode"]) ? DefaultMenuCode() : child["MenuCode"]!.Trim();
                var group = NullIfWhiteSpace(child["GroupCode"]);
                var label = string.IsNullOrWhiteSpace(child["Label"]) ? DefaultLabel(level) : child["Label"]!.Trim();
                var role = NullIfWhiteSpace(child["RoleName"]);
                levels.Add(new ApprovalLevel(level, menu, group, label, role));
            }
        }

        if (levels.Count == 0)
            levels.Add(new ApprovalLevel(1, DefaultMenuCode(), DefaultGroupCode(), "APPROVED OLEH", null));

        return levels.GroupBy(x => x.Level).Select(x => x.First()).OrderBy(x => x.Level).ToList();
    }

    public int MaxLevel(string formCode) => GetLevels(formCode).Max(x => x.Level);
    public ApprovalLevel? GetLevel(string formCode, int level) => GetLevels(formCode).FirstOrDefault(x => x.Level == level);

    private string DefaultMenuCode() => config["Qc:DefaultApprovalMenuCode"] ?? "QC_APPROVAL";
    private string? DefaultGroupCode() => NullIfWhiteSpace(config["Qc:DefaultApprovalGroupCode"]);
    private static string DefaultLabel(int level) => level switch
    {
        1 => "DIPERIKSA OLEH",
        2 => "APPROVED OLEH",
        3 => "DIKETAHUI OLEH",
        _ => $"APPROVAL LEVEL {level}"
    };
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
