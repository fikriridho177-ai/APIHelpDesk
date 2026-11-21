using System.Globalization;
using System.Text.Json;
using AuthUtility.Application.DTOs.Sensus;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Sensus;

// Evaluation uses only persisted master rule/parameter metadata, never a HPT-code switch.
public static class SensusHptEvaluation
{
    public static readonly string[] Operators={">",">=","<","<=","="};
    public static readonly string[] Aggregations={"AVG","SUM","MIN","MAX"};
    public static readonly string[] Numbers={"PERSEN_TERSERANG","POKOK_TERSERANG","POKOK_SAMPEL","LUAS_HA","JUMLAH_POHON","JUMLAH_JALUR"};
    public static readonly string[] Texts={"STATUS_LAHAN","FASE_TANAMAN","JENIS_FORM"};
    public static readonly string[] Colors={"red","green","gray","orange","blue"};
    public static bool Compare(decimal a,string op,decimal b)=>op switch{">"=>a>b,">="=>a>=b,"<"=>a<b,"<="=>a<=b,"="=>a==b,_=>throw new ArgumentException("Operator tidak valid.")};
    private static decimal? Number(SnsHeader h,string code,string aggregate)
    {
        switch(code.ToUpperInvariant())
        {
            case "PERSEN_TERSERANG":return h.JumlahPokokSampel>0?h.JumlahPokokTerserang*100m/h.JumlahPokokSampel:null;
            case "POKOK_TERSERANG":return h.JumlahPokokTerserang;
            case "POKOK_SAMPEL":return h.JumlahPokokSampel;
            case "LUAS_HA":return h.LuasHa;
            case "JUMLAH_POHON":return h.JumlahPokokPersil;
            case "JUMLAH_JALUR":return h.JumlahJalur;
        }
        var values=h.Details.Where(d=>d.Parameter?.Active==true && d.Parameter.Code.Equals(code,StringComparison.OrdinalIgnoreCase) && d.NilaiAngka.HasValue).Select(d=>d.NilaiAngka!.Value).ToList();
        if(values.Count==0)return null;
        return aggregate switch{"AVG"=>values.Average(),"SUM"=>values.Sum(),"MIN"=>values.Min(),"MAX"=>values.Max(),_=>null};
    }
    private static string? Text(SnsHeader h,string code)
    {
        string? value=code.ToUpperInvariant() switch{"STATUS_LAHAN"=>h.StatusLahan,"FASE_TANAMAN"=>h.FaseTanaman,"JENIS_FORM"=>h.JenisForm,_=>null};
        if(value!=null)return value;
        var values=h.Details.Where(d=>d.Parameter?.Active==true && d.Parameter.Code.Equals(code,StringComparison.OrdinalIgnoreCase)).Select(d=>d.NilaiTeks?.Trim()).Where(v=>!string.IsNullOrEmpty(v)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if(values.Count==1)return values[0];
        // Legacy snapshots have phase in an OPTION master rather than a header field.
        if(code=="FASE_TANAMAN")
        {
            var phases=h.Details.Where(d=>d.Parameter?.Active==true && d.Parameter.TipeData=="OPTION" &&
                (d.Parameter.Opsi??"").Split(',').Any(v=>v.Trim().Equals("TM",StringComparison.OrdinalIgnoreCase)) &&
                (d.Parameter.Opsi??"").Split(',').Any(v=>v.Trim().Equals("TBM",StringComparison.OrdinalIgnoreCase)))
                .Select(d=>d.NilaiTeks?.Trim().ToUpperInvariant()).Where(v=>v is "TM" or "TBM").Distinct().ToList();
            return phases.Count==1?phases[0]:null;
        }
        return null;
    }
    private static bool? Condition(SnsHeader h,SnsHptRule r)
    {
        if(r.ConditionParameterCode==null)return true;
        if(!Operators.Contains(r.ConditionOperator))return null;
        if(Numbers.Contains(r.ConditionParameterCode) || h.Details.Any(d=>d.Parameter?.Active==true && d.Parameter.Code.Equals(r.ConditionParameterCode,StringComparison.OrdinalIgnoreCase) && d.Parameter.TipeData=="NUMBER"))
        {
            var value=Number(h,r.ConditionParameterCode,r.Aggregation);
            return value.HasValue && decimal.TryParse(r.ConditionValue?.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out var expected)?Compare(value.Value,r.ConditionOperator!,expected):null;
        }
        var text=Text(h,r.ConditionParameterCode);
        if(text==null||r.ConditionOperator!="=")return null;
        return r.ConditionParameterCode.Equals("STATUS_LAHAN",StringComparison.OrdinalIgnoreCase)
            ?SensusBusinessContext.NormalizeLand(text)==SensusBusinessContext.NormalizeLand(r.ConditionValue)
            :text.Equals(r.ConditionValue,StringComparison.OrdinalIgnoreCase);
    }
    public static string Describe(SnsHptRule r)=>$"{(r.ConditionParameterCode==null?"":$"{r.ConditionParameterCode} {r.ConditionOperator} {r.ConditionValue}: ")}{r.Aggregation} {r.ParameterCode} {r.Operator} {r.ThresholdValue.ToString(CultureInfo.InvariantCulture)} {r.ThresholdUnit}";
    public static SnsEvaluationDto Evaluate(SnsHeader h)
    {
        if(h.HptDetails.Count>0)
        {
            // HPT counts overlap; evaluate each observation against its own master rules.
            // Unknown results rank above monitoring so missing data cannot become green.
            return h.HptDetails.OrderBy(d=>d.HptId)
                .Select(d=>EvaluateSingle(SensusParcelHpt.ForHpt(h,d)))
                .OrderByDescending(e=>e.PerluPengendalian==true?2:e.PerluPengendalian==null?1:0).First();
        }
        return EvaluateSingle(h);
    }
    private static SnsEvaluationDto EvaluateSingle(SnsHeader h)
    {
        var rules=h.Jenis?.Rules.Where(r=>r.Active).OrderBy(r=>r.SortOrder).ThenBy(r=>r.Id).ToList()??new();
        if(rules.Count==0)return new("NONE",null,null,null,"Aturan master belum ditetapkan.",Color:"gray");
        var description=string.Join("; ",rules.Select(Describe));var unknown=false;SnsHptRule? fallback=null;decimal? last=null;
        foreach(var r in rules)
        {
            if(r.StatusLahan!=null && h.StatusLahan==null){unknown=true;continue;}
            if(r.StatusLahan!=null && SensusBusinessContext.NormalizeLand(r.StatusLahan)!=SensusBusinessContext.NormalizeLand(h.StatusLahan) || r.JenisForm!=null && r.JenisForm!=h.JenisForm)continue;
            if(!Operators.Contains(r.Operator)||!Aggregations.Contains(r.Aggregation)||!Colors.Contains(r.Color)||!Colors.Contains(r.ElseColor)||r.ResultCode is not ("PERLU_PENGENDALIAN" or "MONITORING")){unknown=true;continue;}
            var condition=Condition(h,r);if(condition==null){unknown=true;continue;}if(!condition.Value)continue;
            var value=Number(h,r.ParameterCode,r.Aggregation);if(!value.HasValue){unknown=true;continue;}
            if(Compare(value.Value,r.Operator,r.ThresholdValue))
            {
                var control=r.ResultCode=="PERLU_PENGENDALIAN";
                if(!control && unknown)continue;
                return new("MASTER_RULE",control,value,r.ThresholdUnit,description,r.Message,r.ResultCode,r.Color,r.Id);
            }
            if(fallback==null){fallback=r;last=value;}
        }
        if(unknown || fallback==null)return new("MASTER_RULE",null,null,null,description,"Data/kondisi evaluasi belum lengkap atau tidak cocok.",Color:"gray");
        return new("MASTER_RULE",false,last,fallback.ThresholdUnit,description,fallback.ElseMessage,"MONITORING",fallback.ElseColor,fallback.Id);
    }
    public static string? RuleJson(SnsJenis kind)=>kind.Rules.Any(r=>r.Active)?JsonSerializer.Serialize(new{description=string.Join("; ",kind.Rules.Where(r=>r.Active).OrderBy(r=>r.SortOrder).ThenBy(r=>r.Id).Select(Describe))}):null;
    public static async Task<Dictionary<long,SnsEvaluationDto>> EvaluateAsync(AppDbContext db,ICollection<long> ids,int? hptId=null)
    {
        if(ids.Count==0)return new();
        var headers=await db.SnsHeaders.AsNoTracking().Where(h=>ids.Contains(h.Id))
            .Include(h=>h.Jenis).ThenInclude(k=>k!.Rules)
            .Include(h=>h.HptDetails).ThenInclude(d=>d.Hpt).ThenInclude(k=>k!.Rules)
            .Include(h=>h.Details).ThenInclude(d=>d.Parameter).AsSplitQuery().ToListAsync();
        if(hptId==null)return headers.ToDictionary(h=>h.Id,Evaluate);
        return headers.SelectMany(SensusParcelHpt.Expand).Where(h=>h.JenisId==hptId)
            .ToDictionary(h=>h.Id,EvaluateSingle);
    }
}
