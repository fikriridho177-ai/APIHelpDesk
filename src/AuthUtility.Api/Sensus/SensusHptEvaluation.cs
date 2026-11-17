using System.Text.Json;
using System.Text.RegularExpressions;
using AuthUtility.Application.DTOs.Sensus;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Sensus;

// Official workbook Sheet1 C72:C82 and patch README. This is control-required evaluation,
// not an invented mapping to the legacy RINGAN/SEDANG/BERAT severity scale.
public static class SensusHptEvaluation
{
    public record Rule(string EvaluationType,string Description,string SourceCell,decimal? Threshold=null,string? Unit=null);
    public static Rule? Definition(string code,string? name=null)
    {
        static string Clean(string value)=>Regex.Replace(value.ToUpperInvariant(),"[^A-Z0-9]","");
        var key=Clean(code);
        var aliases=new Dictionary<string,string> { ["KUMBANG"]="ORYCTES",["KUMBANGTANDUK"]="ORYCTES",["PLASISPA"]="PLESISPA",["ULATK"]="ULATKANTONG",["BUSUKP"]="BUSUKPUCUK" };
        Rule? Find(string value)
        {
            value=aliases.GetValueOrDefault(value,value);
            return value switch {
                "ORYCTES"=>new("CONDITIONAL","TM >3% pokok terserang; TBM >=1 pohon terserang.","C73",3,"%"),
                "PLESISPA"=>new("CONDITIONAL","TM >5% pokok terserang; TBM >=1 pohon terserang.","C76",5,"%"),
                "ULATAPI" or "ULATKANTONG"=>new("THRESHOLD",">=5 ulat/pelepah memerlukan pengendalian.",value=="ULATAPI"?"C74":"C75",5,"ulat/pelepah"),
                "TIRATHABA"=>new("THRESHOLD",">=5% pokok terserang memerlukan pengendalian.","C77",5,"%"),
                "RAYAP"=>new("THRESHOLD",">=1 pohon terserang memerlukan pengendalian.","C72",1,"pohon"),
                "HELMINTHOSPORIUM" or "FUSARIUM" or "BUSUKPUCUK" or "CURVULARIA" or "PESTALOTIA"=>new("ANY_OCCURRENCE","Setiap ada serangan dilakukan pengendalian.","C78:C82",0,"pohon"),
                _=>null
            };
        }
        return Find(key) ?? Find(Clean((name??"").Replace("Sensus ","",StringComparison.OrdinalIgnoreCase)));
    }

    public static SnsEvaluationDto Evaluate(SnsHeader header)
    {
        var rule=Definition(header.Jenis?.Code??"",header.Jenis?.Name);
        if(rule==null)return new("NONE",null,null,null,"Belum ada aturan pengendalian pada flowchart resmi.");
        SnsEvaluationDto Result(bool? control,decimal? value,string? message=null,string? unit=null)=>new(rule.EvaluationType,control,value,unit??rule.Unit,rule.Description,message);
        var details=header.Details.Where(d=>d.Parameter?.Active==true).ToList();
        decimal? percent=header.JumlahPokokSampel>0?header.JumlahPokokTerserang*100m/header.JumlahPokokSampel:null;
        if(rule.EvaluationType=="CONDITIONAL")
        {
            var stages=details.Where(d=>d.Parameter!.TipeData=="OPTION" &&
                (d.Parameter.Opsi??"").Split(',').Select(x=>x.Trim().ToUpperInvariant()).Contains("TM") &&
                (d.Parameter.Opsi??"").Split(',').Select(x=>x.Trim().ToUpperInvariant()).Contains("TBM"))
                .Select(d=>d.NilaiTeks?.Trim().ToUpperInvariant()).Where(x=>x is "TM" or "TBM").Distinct().ToList();
            if(stages.Count!=1)return Result(null,null,"Isi satu status tanaman TM/TBM yang konsisten melalui parameter OPTION di master.");
            if(stages[0]=="TBM")return Result(header.JumlahPokokTerserang>=1,header.JumlahPokokTerserang,unit:"pohon");
            return Result(percent.HasValue?percent>rule.Threshold:null,percent,percent==null?"Jumlah pokok sampel belum tersedia.":null);
        }
        if(rule.Unit=="ulat/pelepah")
        {
            var indicators=details.Where(d=>d.Parameter!.IsIndikator && d.Parameter.TipeData=="NUMBER" &&
                (d.Parameter.Code.Equals("JML_ULAT",StringComparison.OrdinalIgnoreCase) ||
                 (d.Parameter.Name.Contains("ulat",StringComparison.OrdinalIgnoreCase)&&d.Parameter.Name.Contains("pelepah",StringComparison.OrdinalIgnoreCase))))
                .GroupBy(d=>d.ParameterId).ToList();
            if(indicators.Count!=1)return Result(null,null,"Atur satu indikator jumlah ulat per pelepah pada Master Jenis & Parameter.");
            var values=indicators[0].Where(d=>d.NilaiAngka.HasValue).Select(d=>d.NilaiAngka!.Value).ToList();
            decimal? average=values.Count>0?values.Average():null;
            return Result(average.HasValue?average>=rule.Threshold:null,average);
        }
        if(rule.Unit=="%")return Result(percent.HasValue?percent>=rule.Threshold:null,percent,percent==null?"Jumlah pokok sampel belum tersedia.":null);
        return Result(rule.EvaluationType=="ANY_OCCURRENCE"?header.JumlahPokokTerserang>0:header.JumlahPokokTerserang>=rule.Threshold,header.JumlahPokokTerserang);
    }

    public static async Task<Dictionary<long,SnsEvaluationDto>> EvaluateAsync(AppDbContext db,ICollection<long> ids)
    {
        if(ids.Count==0)return new();
        var headers=await db.SnsHeaders.AsNoTracking().Where(x=>ids.Contains(x.Id)).Include(x=>x.Jenis)
            .Include(x=>x.Details).ThenInclude(x=>x.Parameter).AsSplitQuery().ToListAsync();
        return headers.ToDictionary(x=>x.Id,Evaluate);
    }

    public static string? RuleJson(SnsJenis jenis)
    {
        var definition=Definition(jenis.Code,jenis.Name);
        return definition==null?null:JsonSerializer.Serialize(definition,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
    }
}
