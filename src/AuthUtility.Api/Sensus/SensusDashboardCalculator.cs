using AuthUtility.Application.DTOs.Sensus;
using AuthUtility.Domain.Entities;

namespace AuthUtility.Api.Sensus;

public record SensusDashboardDto(int Total, int Submitted, int Approved, int Revisi, int Rejected,
    int Berat, int PersilTerlambat, string? PersilTerlambatError, List<SnsCountDto> PerJenis, List<SnsCountDto> PerWilayah,
    Dictionary<string, int> PerTingkat, List<SensusMonthlyCount> TrenBulanan, List<SensusTopPersil> TopPersil);
public record SensusMonthlyCount(string Periode, int Total);
public record SensusTopPersil(int JenisId, string JenisName, string PersilSumber, string PersilId, string PersilKode,
    string? KodeWilayah, string Tingkat, decimal PersenTerserang, decimal? RataIndikator);

public static class SensusDashboardCalculator
{
    public static SensusDashboardDto Build(SnsMonitoringDto monitoring, IReadOnlyList<SnsHeader> headers,
        IReadOnlyDictionary<long, (string Tingkat, decimal? Rata)> evaluated)
    {
        var levels = SnsTingkat.Urutan.ToDictionary(level => level, _ => 0);
        foreach (var header in headers.Where(x => x.Status == SnsStatus.Approved))
            levels[header.HptDetails.Count>0 ? SensusParcelHpt.Level(header).Tingkat :
                evaluated.TryGetValue(header.Id, out var value) ? value.Tingkat : SnsTingkat.Kosong]++;
        var trend = headers.GroupBy(x => x.TanggalSensus.ToString("yyyy-MM"))
            .OrderBy(x => x.Key).Select(x => new SensusMonthlyCount(x.Key, x.Count())).ToList();
        var childHeaderIds = headers.Where(h=>h.HptDetails.Count>0).Select(h=>h.Id).ToHashSet();
        (string Tingkat, decimal? Rata) Level(SnsHeader observation)
        {
            if(!childHeaderIds.Contains(observation.Id))
                return evaluated.TryGetValue(observation.Id,out var legacy)?legacy:(SnsTingkat.Kosong,null);
            var indicators=observation.Details.Where(d=>d.Parameter?.IsIndikator==true&&d.NilaiAngka!=null)
                .GroupBy(d=>d.ParameterId).OrderBy(g=>g.Key).Select(g=>new
                {
                    Rata=g.Average(d=>d.NilaiAngka),
                    Parameter=g.First().Parameter!
                }).ToList();
            var result=SensusHptEvaluation.Evaluate(observation);
            var tingkat=result.PerluPengendalian==true?SnsTingkat.Berat:
                result.PerluPengendalian==false?(observation.JumlahPokokTerserang==0?SnsTingkat.Nihil:SnsTingkat.Ringan):
                SnsTingkat.Max(indicators.Select(i=>SnsTingkat.Hitung(i.Rata,i.Parameter.AmbangSedang,i.Parameter.AmbangBerat)));
            return(tingkat,indicators.FirstOrDefault()?.Rata);
        }
        var top = headers.Where(x => x.Status == SnsStatus.Approved).SelectMany(SensusParcelHpt.Expand)
            .GroupBy(x => new { x.JenisId, x.PersilSumber, x.PersilId })
            .Select(group => group.OrderByDescending(x => x.TanggalSensus).ThenByDescending(x => x.Id).First())
            .Select(header => new SensusTopPersil(header.JenisId??0, header.Jenis?.Name ?? "", header.PersilSumber,
                header.PersilId, header.PersilKode, header.KodeWilayah,
                Level(header).Tingkat,
                header.JumlahPokokSampel == 0 ? 0 : Math.Round(header.JumlahPokokTerserang * 100m / header.JumlahPokokSampel, 2),
                Level(header).Rata))
            .OrderByDescending(x => Array.IndexOf(SnsTingkat.Urutan, x.Tingkat)).ThenByDescending(x => x.PersenTerserang).Take(10).ToList();
        return new(monitoring.Total, monitoring.Submitted, monitoring.Approved, monitoring.Revisi, monitoring.Rejected,
            levels[SnsTingkat.Berat], monitoring.PersilTerlambat.Count, monitoring.PersilTerlambatError,
            monitoring.PerJenis, monitoring.PerWilayah, levels, trend, top);
    }
}
