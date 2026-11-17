using System.Globalization;
using AuthUtility.Application.DTOs.Sensus;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Sensus;

public class SensusBusinessFilter
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public string? Wilayah { get; set; }
    public string? StatusLahan { get; set; }
    public string? JenisForm { get; set; }
    public int? JenisId { get; set; }
}
public record SnsReportKind(int Id,string Code,string Name,string Group);
public record SnsReportParameter(string Code,string Name,string? Satuan,string TipeData);
public record SnsReportSample(int No,Dictionary<string,object?> Values);
public record SnsReportObservation(long Id,string? SensusNo,string Periode,DateOnly Tanggal,int JenisId,string JenisName,string ReportGroup,
    string PersilSumber,string PersilId,string PersilKode,string? Wilayah,string? Afdeling,string StatusLahan,string JenisForm,
    int Sampel,int Terserang,decimal? Persen,SnsBusinessContextDto Context,SnsEvaluationDto Evaluation,
    string Petugas,string? ApprovedBy,DateTime? ApprovedAt,List<SnsReportParameter> Parameters,List<SnsReportSample> Details);
public record SnsReportMatrix(string Periode,string? Wilayah,string? Afdeling,string PersilSumber,string PersilId,string PersilKode,string StatusLahan,string JenisForm,
    decimal? LuasHa,int? Populasi,string? BulanTanam,int? Sampel,int? Jalur,int? MatiPetir,int? MatiHpt,int? MatiLain,string? Pengendalian,string? MusuhAlami,string? Cuaca,
    List<SnsReportObservation> Hpt,List<string> Notes);
public record SnsReportMemo(string Periode,int JenisId,string JenisName,string JenisForm,string StatusLahan,int Persil,int Sampel,int Terserang,decimal? Persen,
    int PerluPengendalian,int BelumDievaluasi,string Recommendation,string Color);
public record SnsReportAggregate(string Key,int Pengamatan,int Persil,int DiAtasAmbang,int BelumDievaluasi);
public record SnsBusinessAnalytics(List<SnsReportAggregate> PerStatusLahan,List<SnsReportAggregate> PerWilayah,List<SnsReportAggregate> PerHpt,List<SnsReportAggregate> PerBulan,List<SnsReportObservation> DiAtasAmbang);
public record SnsBusinessReport(DateOnly DateFrom,DateOnly DateTo,string? Wilayah,string? StatusLahan,string? JenisForm,int? JenisId,
    int ApprovedBeforeDedup,List<SnsReportKind> Jenis,List<SnsReportObservation> Observations,List<SnsReportMatrix> Rekap,List<SnsReportMemo> Memo,SnsBusinessAnalytics Analysis,List<string> Notes);

public static class SensusBusinessReportQuery
{
    public const int MaxRecords=5000;
    public static (DateOnly From,DateOnly To) Period(SensusBusinessFilter filter)
    {
        var today=DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        return(filter.DateFrom??new DateOnly(today.Year,today.Month,1),filter.DateTo??today);
    }
    public static string? Validate(SensusBusinessFilter f)
    {
        var(from,to)=Period(f);
        if(from>to||to.DayNumber-from.DayNumber>=366)return "Periode report harus berurutan dan maksimal 366 hari.";
        if(f.JenisForm!=null && f.JenisForm is not ("FORM1" or "FORM2"))return "Jenis form tidak valid.";
        if(f.Wilayah?.Length>100||f.StatusLahan?.Length>50||f.JenisId<1)return "Filter report tidak valid.";
        return null;
    }
    public static IQueryable<SnsHeader> ApprovedQuery(AppDbContext db,SensusBusinessFilter f)
    {
        var(from,to)=Period(f);var q=db.SnsHeaders.AsNoTracking().Where(h=>h.Status==SnsStatus.Approved&&h.TanggalSensus>=from&&h.TanggalSensus<=to);
        if(!string.IsNullOrWhiteSpace(f.Wilayah))q=q.Where(h=>h.KodeWilayah==f.Wilayah.Trim());
        if(f.JenisId!=null)q=q.Where(h=>h.JenisId==f.JenisId);
        if(f.JenisForm!=null)q=q.Where(h=>h.JenisForm==f.JenisForm);
        var land=SensusBusinessContext.NormalizeLand(f.StatusLahan);
        if(land=="BELUM_DIKETAHUI")q=q.Where(h=>h.StatusLahan==null||h.StatusLahan=="");
        else if(land!=null)q=q.Where(h=>h.StatusLahan==land);
        return q;
    }
    public static async Task<SnsBusinessReport?> ReadAsync(AppDbContext db,SensusBusinessFilter filter,CancellationToken ct=default)
    {
        var q=ApprovedQuery(db,filter);
        if(await q.CountAsync(ct)>MaxRecords)return null;
        var headers=await q.Include(h=>h.Jenis).ThenInclude(k=>k!.Rules).Include(h=>h.Details).ThenInclude(d=>d.Parameter)
            .Include(h=>h.Petugas).Include(h=>h.ApprovedBy).AsSplitQuery().ToListAsync(ct);
        var kinds=await db.SnsJenis.AsNoTracking().Where(k=>k.Active||q.Any(h=>h.JenisId==k.Id)).OrderBy(k=>k.ReportGroup).ThenBy(k=>k.Name).ThenBy(k=>k.Id)
            .Select(k=>new SnsReportKind(k.Id,k.Code,k.Name,k.ReportGroup)).ToListAsync(ct);
        return Build(headers,kinds,filter);
    }
    public static SnsBusinessReport Build(IEnumerable<SnsHeader> source,List<SnsReportKind> kinds,SensusBusinessFilter filter)
    {
        var(from,to)=Period(filter);var land=SensusBusinessContext.NormalizeLand(filter.StatusLahan);
        var approved=source.Where(h=>h.Status==SnsStatus.Approved&&h.TanggalSensus>=from&&h.TanggalSensus<=to)
            .Where(h=>string.IsNullOrWhiteSpace(filter.Wilayah)||h.KodeWilayah==filter.Wilayah.Trim())
            .Where(h=>filter.JenisId==null||h.JenisId==filter.JenisId).Where(h=>filter.JenisForm==null||h.JenisForm==filter.JenisForm)
            .Where(h=>land==null||(land=="BELUM_DIKETAHUI"?string.IsNullOrEmpty(h.StatusLahan):h.StatusLahan==land)).ToList();
        // One latest approved observation per HPT/parcel/form/land/month. No double-counted rechecks.
        var selected=approved.GroupBy(h=>new{h.PersilSumber,h.PersilId,h.JenisId,h.JenisForm,h.StatusLahan,Month=h.TanggalSensus.ToString("yyyy-MM")})
            .Select(g=>g.OrderByDescending(h=>h.TanggalSensus).ThenByDescending(h=>h.Id).First()).ToList();
        var observations=selected.Select(h=>
        {
            var parameters=h.Details.Where(d=>d.Parameter!=null).Select(d=>d.Parameter!).DistinctBy(p=>p.Code).OrderBy(p=>p.Id)
                .Select(p=>new SnsReportParameter(p.Code,p.Name,p.Satuan,p.TipeData)).ToList();
            var details=h.Details.Where(d=>d.Parameter!=null).GroupBy(d=>d.NoPokok??0).OrderBy(g=>g.Key)
                .Select(g=>new SnsReportSample(g.Key,g.GroupBy(d=>d.Parameter!.Code).ToDictionary(p=>p.Key,p=>(object?)p.Last().NilaiAngka??p.Last().NilaiTeks))).ToList();
            return new SnsReportObservation(h.Id,h.SensusNo,h.TanggalSensus.ToString("yyyy-MM"),h.TanggalSensus,h.JenisId,h.Jenis?.Name??"",h.Jenis?.ReportGroup??"LAINNYA",
                h.PersilSumber,h.PersilId,h.PersilKode,h.KodeWilayah,h.KodeAfdeling,string.IsNullOrWhiteSpace(h.StatusLahan)?"BELUM_DIKETAHUI":h.StatusLahan,h.JenisForm,h.JumlahPokokSampel,h.JumlahPokokTerserang,
                Percent(h.JumlahPokokTerserang,h.JumlahPokokSampel),SensusBusinessContext.Dto(h),SensusHptEvaluation.Evaluate(h),h.Petugas?.Name??"",h.ApprovedBy?.Name,h.ApprovedAt,parameters,details);
        }).OrderBy(o=>o.Periode).ThenBy(o=>o.Wilayah).ThenBy(o=>o.PersilKode).ThenBy(o=>o.PersilSumber).ThenBy(o=>o.PersilId)
            .ThenBy(o=>o.JenisForm).ThenBy(o=>o.StatusLahan).ThenBy(o=>o.JenisId).ToList();
        var matrix=observations.GroupBy(o=>new{o.Periode,o.Wilayah,o.Afdeling,o.PersilSumber,o.PersilId,o.PersilKode,o.StatusLahan,o.JenisForm}).Select(g=>
        {
            var notes=new List<string>();
            T? One<T>(IEnumerable<T?> values,string label)where T:struct
            {var unique=values.Where(v=>v.HasValue).Distinct().ToList();if(unique.Count>1)notes.Add(label+" berbeda antar HPT; lihat detail per HPT.");return unique.Count==1?unique[0]:null;}
            string? Text(IEnumerable<string?> values)=>string.Join("; ",values.Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct());
            var a=g.Key;
            return new SnsReportMatrix(a.Periode,a.Wilayah,a.Afdeling,a.PersilSumber,a.PersilId,a.PersilKode,a.StatusLahan,a.JenisForm,
                One(g.Select(o=>o.Context.LuasHa),"Luas"),One(g.Select(o=>o.Context.JumlahPokokPersil),"Populasi"),Text(g.Select(o=>o.Context.BulanTanam)),
                One(g.Select(o=>(int?)o.Sampel),"Denominator sampel"),One(g.Select(o=>o.Context.JumlahJalur),"Jalur"),One(g.Select(o=>o.Context.PohonMatiPetir),"Pohon mati petir"),
                One(g.Select(o=>o.Context.PohonMatiHpt),"Pohon mati HPT"),One(g.Select(o=>o.Context.PohonMatiLain),"Pohon mati lainnya"),Text(g.Select(o=>o.Context.Pengendalian)),
                Text(g.Select(o=>o.Context.MusuhAlami)),Text(g.Select(o=>o.Context.Cuaca)),g.ToList(),notes);
        }).ToList();
        var memo=observations.GroupBy(o=>new{o.Periode,o.JenisId,o.JenisName,o.JenisForm,o.StatusLahan}).Select(g=>
        {
            var chosen=g.FirstOrDefault(o=>o.Evaluation.PerluPengendalian==true)??g.FirstOrDefault(o=>o.Evaluation.PerluPengendalian==null)??g.First();
            return new SnsReportMemo(g.Key.Periode,g.Key.JenisId,g.Key.JenisName,g.Key.JenisForm,g.Key.StatusLahan,g.Select(Parcel).Distinct().Count(),g.Sum(o=>o.Sampel),g.Sum(o=>o.Terserang),
                Percent(g.Sum(o=>o.Terserang),g.Sum(o=>o.Sampel)),g.Count(o=>o.Evaluation.PerluPengendalian==true),g.Count(o=>o.Evaluation.PerluPengendalian==null),
                string.Join("; ",g.Select(o=>o.Evaluation.Message??o.Evaluation.Description).Distinct()),chosen.Evaluation.Color??"gray");
        }).ToList();
        List<SnsReportAggregate> Group(Func<SnsReportObservation,string> key)=>observations.GroupBy(key).OrderBy(g=>g.Key)
            .Select(g=>new SnsReportAggregate(g.Key,g.Count(),g.Select(Parcel).Distinct().Count(),g.Count(o=>o.Evaluation.PerluPengendalian==true),g.Count(o=>o.Evaluation.PerluPengendalian==null))).ToList();
        var analytics=new SnsBusinessAnalytics(Group(o=>o.StatusLahan),Group(o=>o.Wilayah??"Belum diisi"),Group(o=>o.JenisName),Group(o=>o.Periode),observations.Where(o=>o.Evaluation.PerluPengendalian==true).ToList());
        var reportNotes=new List<string>{"Hanya transaksi APPROVED. Satu observasi terakhir per persil/HPT/form/status lahan/bulan; hasil evaluasi memakai rule master saat report dibuka.",
            "Sampel dan persentase dihitung per HPT. Populasi/luas/pohon mati tidak dijumlahkan berulang antar HPT. Nilai kosong tidak dianggap nol."};
        if(observations.Any(o=>o.StatusLahan=="BELUM_DIKETAHUI"))reportNotes.Add("Sebagian data lama belum memiliki snapshot status lahan; tidak diasumsikan Organik/Non-Organik.");
        return new(from,to,filter.Wilayah,land,filter.JenisForm,filter.JenisId,approved.Count,kinds,observations,matrix,memo,analytics,reportNotes);
    }
    private static string Parcel(SnsReportObservation o)=>o.PersilSumber+"|"+o.PersilId;
    private static decimal? Percent(int count,int sample)=>sample>0?decimal.Round(count*100m/sample,4):null;
}
