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
            levels[evaluated.TryGetValue(header.Id, out var value) ? value.Tingkat : SnsTingkat.Kosong]++;
        var trend = headers.GroupBy(x => x.TanggalSensus.ToString("yyyy-MM"))
            .OrderBy(x => x.Key).Select(x => new SensusMonthlyCount(x.Key, x.Count())).ToList();
        var top = headers.Where(x => x.Status == SnsStatus.Approved)
            .GroupBy(x => new { x.JenisId, x.PersilSumber, x.PersilId })
            .Select(group => group.OrderByDescending(x => x.TanggalSensus).ThenByDescending(x => x.Id).First())
            .Select(header => new SensusTopPersil(header.JenisId, header.Jenis?.Name ?? "", header.PersilSumber,
                header.PersilId, header.PersilKode, header.KodeWilayah,
                evaluated.TryGetValue(header.Id, out var value) ? value.Tingkat : SnsTingkat.Kosong,
                header.JumlahPokokSampel == 0 ? 0 : Math.Round(header.JumlahPokokTerserang * 100m / header.JumlahPokokSampel, 2),
                evaluated.TryGetValue(header.Id, out value) ? value.Rata : null))
            .OrderByDescending(x => Array.IndexOf(SnsTingkat.Urutan, x.Tingkat)).ThenByDescending(x => x.PersenTerserang).Take(10).ToList();
        return new(monitoring.Total, monitoring.Submitted, monitoring.Approved, monitoring.Revisi, monitoring.Rejected,
            levels[SnsTingkat.Berat], monitoring.PersilTerlambat.Count, monitoring.PersilTerlambatError,
            monitoring.PerJenis, monitoring.PerWilayah, levels, trend, top);
    }
}
