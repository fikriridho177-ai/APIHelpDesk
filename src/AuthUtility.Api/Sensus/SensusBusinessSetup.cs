using System.Text.RegularExpressions;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Sensus;

public static class SensusBusinessSetup
{
    public static async Task EnsureAsync(AppDbContext db)
    {
        if(db.Database.ProviderName=="Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            using var stream=typeof(SensusBusinessSetup).Assembly.GetManifestResourceStream("Sensus.BusinessSchema.sql")
                ??throw new InvalidOperationException("Schema Sensus tidak ditemukan.");
            using var reader=new StreamReader(stream);
            await db.Database.ExecuteSqlRawAsync(await reader.ReadToEndAsync());
        }
        await SeedAsync(db);
    }

    public static async Task SeedAsync(AppDbContext db)
    {
        await using var tx=db.Database.IsRelational()?await db.Database.BeginTransactionAsync():null;
        if(db.Database.ProviderName=="Npgsql.EntityFrameworkCore.PostgreSQL")await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(20261117, 2)");
        var kinds=await db.SnsJenis.Include(k=>k.Parameters).Include(k=>k.Rules).AsSplitQuery().Where(k=>k.BusinessSeedVersion==0).ToListAsync();
        foreach(var kind in kinds)
        {
            static string Canonical(string value)
            {
                var code=Regex.Replace(value.ToUpperInvariant(),"[^A-Z0-9]","");
                return code switch{"PLASISPA" or "PLASSISPA"=>"PLESISPA","KUMBANG" or "KUMBANGTANDUK"=>"ORYCTES","ULATK"=>"ULATKANTONG","BUSUKP"=>"BUSUKPUCUK",_=>code};
            }
            var key=Canonical(kind.Code);
            if(!Known.Contains(key))key=Canonical(kind.Name.Replace("Sensus ","",StringComparison.OrdinalIgnoreCase));
            var disease=Diseases.Contains(key);
            if(Known.Contains(key))kind.ReportGroup=disease?"PENYAKIT":"HAMA";
            kind.Form2Enabled |= key is "PLESISPA" or "ULATAPI";
            SnsParameter Ensure(string code,string name,string type="NUMBER",string? options=null,string scope="FORM2",bool indicator=false,string? unit=null)
            {
                var p=kind.Parameters.FirstOrDefault(p=>p.Code.Equals(code,StringComparison.OrdinalIgnoreCase));
                if(p!=null)return p;
                p=new(){JenisId=kind.Id,Code=code,Name=name,TipeData=type,Opsi=options,FormScope=scope,Active=true,IsIndikator=indicator,Satuan=unit};
                kind.Parameters.Add(p);return p;
            }
            if(kind.Form2Enabled)
            {
                Ensure("NO_JALUR","No. jalur");Ensure("NO_POHON","No. pohon");Ensure("CATATAN_POKOK","Catatan/saran pengamatan","TEXT");
                if(key=="ULATAPI")
                {
                    Ensure("JENIS_ULAT","Jenis ulat","OPTION","SA,SN,DT,DD,UA,MC,MP,PP,UK");
                    Ensure("TELUR","Jumlah telur");Ensure("ULAT_KECIL","Ulat hidup kecil (<5 mm)");Ensure("ULAT_BESAR","Ulat hidup besar (>5 mm)");
                    Ensure("ULAT_MATI","Jumlah ulat mati");Ensure("PUPA_HIDUP","Pupa hidup");Ensure("PUPA_KOSONG_MATI","Pupa kosong/mati");Ensure("NO_PELEPAH","No. pelepah");
                }
            }
            if(kind.Rules.Count==0 && Known.Contains(key))
            {
                SnsHptRule Rule(string code,string op,decimal threshold,string unit,string? stage=null)=>new(){JenisId=kind.Id,ParameterCode=code,Operator=op,ThresholdValue=threshold,ThresholdUnit=unit,
                    ConditionParameterCode=stage==null?null:"FASE_TANAMAN",ConditionOperator=stage==null?null:"=",ConditionValue=stage,
                    Message=$"{kind.Name}: ambang terpenuhi, lakukan pengendalian.",ElseMessage="Tetap lakukan monitoring karena serangan di bawah ambang batas.",Color="red",ElseColor="green",SeedKey=$"flowchart-v1:{kind.Id}:{stage??"ALL"}"};
                if(key is "PLESISPA" or "ORYCTES")
                {
                    kind.Rules.Add(Rule("PERSEN_TERSERANG",">",key=="ORYCTES"?3:5,"%","TM"));
                    kind.Rules.Add(Rule("POKOK_TERSERANG",">=",1,"pohon","TBM"));
                }
                else if(key is "ULATAPI" or "ULATKANTONG")
                {
                    var indicators=kind.Parameters.Where(p=>p.Active && p.TipeData=="NUMBER" && p.IsIndikator &&
                        (p.Code.Equals("JML_ULAT",StringComparison.OrdinalIgnoreCase) || p.Name.Contains("ulat",StringComparison.OrdinalIgnoreCase)&&p.Name.Contains("pelepah",StringComparison.OrdinalIgnoreCase))).ToList();
                    var parameter=indicators.Count==1?indicators[0]:Ensure("JML_ULAT","Jumlah ulat per pelepah",scope:"ALL",indicator:true,unit:"ulat/pelepah");
                    // Do not alter existing parameter definitions or infer a per-frond value from whole-tree counts.
                    if(parameter.Active && parameter.TipeData=="NUMBER")kind.Rules.Add(Rule(parameter.Code,">=",5,"ulat/pelepah"));
                }
                else kind.Rules.Add(Rule(key=="TIRATHABA"?"PERSEN_TERSERANG":"POKOK_TERSERANG",disease?">":">=",disease?0:key=="RAYAP"?1:5,key=="TIRATHABA"?"%":"pohon"));
            }
            kind.BusinessSeedVersion=1;
        }
        await db.SaveChangesAsync();if(tx!=null)await tx.CommitAsync();
    }
    private static readonly string[] Diseases={"HELMINTHOSPORIUM","FUSARIUM","BUSUKPUCUK","CURVULARIA","PESTALOTIA"};
    private static readonly HashSet<string> Known=new(Diseases.Concat(new[]{"PLESISPA","ORYCTES","ULATAPI","ULATKANTONG","RAYAP","TIRATHABA"}));
}
