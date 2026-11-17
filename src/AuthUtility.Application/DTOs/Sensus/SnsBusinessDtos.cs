namespace AuthUtility.Application.DTOs.Sensus;

public class SnsBusinessInput
{
    public string? JenisForm { get; set; }
    public string? StatusLahan { get; set; }
    public string? FaseTanaman { get; set; }
    public decimal? LuasHa { get; set; }
    public int? JumlahPokokPersil { get; set; }
    public string? BulanTanam { get; set; }
    public int? JumlahJalur { get; set; }
    public int? PohonMatiPetir { get; set; }
    public int? PohonMatiHpt { get; set; }
    public int? PohonMatiLain { get; set; }
    public string? Pengendalian { get; set; }
    public string? MusuhAlami { get; set; }
    public string? Cuaca { get; set; }
}

public record SnsBusinessContextDto(string JenisForm,string? StatusLahan,string? StatusLahanSumber,string? FaseTanaman,
    decimal? LuasHa,int? JumlahPokokPersil,string? BulanTanam,int? JumlahJalur,int? PohonMatiPetir,int? PohonMatiHpt,int? PohonMatiLain,
    string? Pengendalian,string? MusuhAlami,string? Cuaca);

public class SaveSnsRuleRequest
{
    public string ParameterCode { get; set; } = "PERSEN_TERSERANG";
    public string Operator { get; set; } = ">=";
    public decimal ThresholdValue { get; set; }
    public string ThresholdUnit { get; set; } = "%";
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
}

public record SnsRuleDto(int Id,int JenisId,string ParameterCode,string Operator,decimal ThresholdValue,string ThresholdUnit,string Aggregation,
    string? ConditionParameterCode,string? ConditionOperator,string? ConditionValue,string? StatusLahan,string? JenisForm,
    string ResultCode,string Message,string Color,string ElseMessage,string ElseColor,bool Active,int SortOrder);
