namespace AuthUtility.Domain.Entities;

public class SnsHptRule
{
    public int Id { get; set; }
    public int JenisId { get; set; }
    public string ParameterCode { get; set; } = "";
    public string Operator { get; set; } = ">=";
    public decimal ThresholdValue { get; set; }
    public string ThresholdUnit { get; set; } = "";
    public string Aggregation { get; set; } = "AVG";
    public string? ConditionParameterCode { get; set; }
    public string? ConditionOperator { get; set; }
    public string? ConditionValue { get; set; }
    public string? StatusLahan { get; set; }
    public string? JenisForm { get; set; }
    public string ResultCode { get; set; } = "PERLU_PENGENDALIAN";
    public string Message { get; set; } = "";
    public string Color { get; set; } = "red";
    public string ElseMessage { get; set; } = "";
    public string ElseColor { get; set; } = "green";
    public bool Active { get; set; } = true;
    public int SortOrder { get; set; }
    public string? SeedKey { get; set; }
}
