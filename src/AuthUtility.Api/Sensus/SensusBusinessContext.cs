using System.Globalization;
using AuthUtility.Application.DTOs.Sensus;
using AuthUtility.Domain.Entities;

namespace AuthUtility.Api.Sensus;

public record SensusPersilContext(string? StatusLahan,string? FaseTanaman,decimal? LuasHa,int? JumlahPokokPersil,string? BulanTanam);

public static class SensusBusinessContext
{
    public static string? NormalizeLand(string? value)
    {
        if(string.IsNullOrWhiteSpace(value))return null;
        var key=value.Trim().ToUpperInvariant().Replace('-','_').Replace(' ','_');
        return key switch {"O" or "ORG" or "ORGANIC" or "ORGANIK"=>"ORGANIK",
            "N" or "NON_ORG" or "NONORGANIK" or "NON_ORGANIC" or "NON_ORGANIK"=>"NON_ORGANIK",
            "R" or "RA" or "RAINFOREST_ALLIANCE"=>"RA",_=>key};
    }

    public static SensusPersilContext FromPersil(IReadOnlyDictionary<string,object?> source)
    {
        var row=new Dictionary<string,object?>(source,StringComparer.OrdinalIgnoreCase);
        object? Raw(params string[] keys)=>keys.Select(k=>row.GetValueOrDefault(k)).FirstOrDefault(v=>v!=null && v!=DBNull.Value && !string.IsNullOrWhiteSpace(Convert.ToString(v,CultureInfo.InvariantCulture)));
        string? Text(params string[] keys)=>Convert.ToString(Raw(keys),CultureInfo.InvariantCulture)?.Trim() is {Length:>0} s?s:null;
        decimal? Number(params string[] keys)
        {
            var raw=Raw(keys);if(raw==null)return null;
            return decimal.TryParse(Convert.ToString(raw,CultureInfo.InvariantCulture)?.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out var n)&&n>=0?n:null;
        }
        var status=NormalizeLand(Text("StatusLahan","JenisLahan","StatusOrganik","OrganicStatus","KodeStatusLahan"));
        if(status==null && Raw("IsOrganic","IsOrganik","Organik") is { } organic)
        {
            var s=Convert.ToString(organic,CultureInfo.InvariantCulture)?.ToLowerInvariant();
            status=s is "true" or "1" or "ya"?"ORGANIK":s is "false" or "0" or "tidak"?"NON_ORGANIK":NormalizeLand(s);
        }
        var phase=Text("FaseTanaman","StatusTanaman","TM_TBM","StatusTM")?.ToUpperInvariant();
        if(phase is not ("TM" or "TBM"))phase=null;
        var population=Number("JumlahPokok","JumlahPohon","Populasi","JlhPohon","JmlPohon");
        var rawPlant=Raw("BulanTanam","TanggalTanam","TglTanam","BlnTanam");
        var planting=rawPlant is DateTime date?date.ToString("yyyy-MM"):Convert.ToString(rawPlant,CultureInfo.InvariantCulture)?.Trim();
        return new(status,phase,Number("LuasHa","Luas","LuasNetto","LuasTanam"),population is >=0 and <=int.MaxValue && population==decimal.Truncate(population.Value)?(int)population.Value:null,planting);
    }

    public static string Form(SnsBusinessInput? input,SnsHeader? existing=null)=>(input?.JenisForm??existing?.JenisForm??"FORM1").Trim().ToUpperInvariant();
    public static bool ParameterFor(SnsParameter p,string form)=>p.Active && (p.FormScope=="ALL" || p.FormScope==form);
    public static string? Validate(SnsBusinessInput? input,SnsJenis jenis,string form)
    {
        if(form is not ("FORM1" or "FORM2") || form=="FORM2"&&!jenis.Form2Enabled)return "Jenis form tidak diizinkan untuk HPT ini.";
        if(input==null)return null;
        if(input.LuasHa<0 || input.JumlahPokokPersil<0 || input.JumlahJalur<0 || input.PohonMatiPetir<0 || input.PohonMatiHpt<0 || input.PohonMatiLain<0)return "Luas dan jumlah pengamatan tidak boleh negatif.";
        if(input.LuasHa>99999999999999.9999m || input.LuasHa.HasValue&&decimal.Round(input.LuasHa.Value,4)!=input.LuasHa)return "Luas maksimal 14 digit dan 4 angka desimal.";
        if(input.StatusLahan?.Length>50 || input.FaseTanaman?.Length>10 || input.BulanTanam?.Length>40 || input.Pengendalian?.Length>2000 || input.MusuhAlami?.Length>500 || input.Cuaca?.Length>50)return "Konteks sensus terlalu panjang.";
        if(!string.IsNullOrWhiteSpace(input.FaseTanaman) && input.FaseTanaman.Trim().ToUpperInvariant() is not ("TM" or "TBM"))return "Fase tanaman harus TM atau TBM.";
        return null;
    }

    public static void Apply(SnsHeader header,SnsBusinessInput? input,SensusPersilContext? master)
    {
        header.JenisForm=Form(input,header);
        if(master?.StatusLahan!=null){header.StatusLahan=master.StatusLahan;header.StatusLahanSumber="PERSIL";}
        else if(input!=null){header.StatusLahan=NormalizeLand(input.StatusLahan);header.StatusLahanSumber=header.StatusLahan==null?null:"MANUAL";}
        header.FaseTanaman=master?.FaseTanaman??(input==null?header.FaseTanaman:input.FaseTanaman?.Trim().ToUpperInvariant());
        header.LuasHa=master?.LuasHa??(input==null?header.LuasHa:input.LuasHa);
        header.JumlahPokokPersil=master?.JumlahPokokPersil??(input==null?header.JumlahPokokPersil:input.JumlahPokokPersil);
        header.BulanTanam=master?.BulanTanam??(input==null?header.BulanTanam:input.BulanTanam?.Trim());
        if(input==null)return; // Existing Android clients may omit the new optional context entirely.
        header.JumlahJalur=input.JumlahJalur;
        header.PohonMatiPetir=input.PohonMatiPetir;
        header.PohonMatiHpt=input.PohonMatiHpt;
        header.PohonMatiLain=input.PohonMatiLain;
        header.Pengendalian=input.Pengendalian?.Trim();
        header.MusuhAlami=input.MusuhAlami?.Trim();
        header.Cuaca=input.Cuaca?.Trim();
    }

    public static SnsBusinessContextDto Dto(SnsHeader h)=>new(h.JenisForm,h.StatusLahan,h.StatusLahanSumber,h.FaseTanaman,h.LuasHa,h.JumlahPokokPersil,h.BulanTanam,h.JumlahJalur,
        h.PohonMatiPetir,h.PohonMatiHpt,h.PohonMatiLain,h.Pengendalian,h.MusuhAlami,h.Cuaca);
}

public partial class PersilReader
{
    private static Persil ForSensus(Persil p)=>p with{Sensus=SensusBusinessContext.FromPersil(p.Detail??new()),Detail=null};
    public async Task<List<Persil>> ListSensusAsync(Source source,string? wilayah=null)=>
        (await ListAsync(source,wilayah,withDetail:true)).Select(ForSensus).ToList();
    public async Task<Persil?> FindSensusAsync(Source source,string id)=>
        (await QueryPersil(source,"TOP (1)","NotActive = 0 AND CAST(PersilID AS varchar(100)) = @persilId",new(){["@persilId"]=id.Trim()},true))
        .Select(ForSensus).FirstOrDefault();
}
