using AuthUtility.Api.Layanan;
using AuthUtility.Domain.Entities;

namespace AuthUtility.Api.Dashboards;

public record DashboardCount(string Key, int Total);
public record DashboardMonth(string Month, int Total);
public record DashboardKind(int Id, string Code, string Name);
public record DashboardHistory(long Id, string? RequestNo, string Title, string Status, DateTime CreatedAt, DateOnly? NeededDate);
public record DivisionDashboardDto(string Division, string Name, DateOnly DateFrom, DateOnly DateTo,
    int ActiveKinds, ReportGroup Summary, List<ReportGroup> PerJenis, List<DashboardCount> PerStatus,
    List<DashboardMonth> PerBulan, List<DashboardKind> JenisAktif, List<DashboardHistory> Latest,
    string? AvailabilityNote);

public static class DivisionDashboardCalculator
{
    public static DivisionDashboardDto Build(string division, string name, DateOnly from, DateOnly to,
        IEnumerable<ReportRow> source, List<DashboardKind> kinds, DateOnly today)
    {
        var rows=source.ToList();
        var report=LynReportCalculator.Build(rows,today);
        var counts=rows.GroupBy(r=>r.Request.Status).ToDictionary(g=>g.Key,g=>g.Count());
        var known=new[]{LynStatus.Baru,LynStatus.Diterima,LynStatus.Dijadwalkan,LynStatus.Diproses,LynStatus.MenungguInfo,LynStatus.Selesai,LynStatus.Ditolak,LynStatus.Dibatalkan};
        var status=known.Concat(counts.Keys).Distinct().Select(s=>new DashboardCount(s,counts.GetValueOrDefault(s))).ToList();
        var byMonth=report.PerBulan.ToDictionary(m=>m.Month,m=>m.Masuk);
        var months=new List<DashboardMonth>();
        for(var month=new DateOnly(from.Year,from.Month,1);month<=to;)
        {
            var key=month.ToString("yyyy-MM"); months.Add(new(key,byMonth.GetValueOrDefault(key)));
            if(month.Year==to.Year && month.Month==to.Month) break;
            month=month.AddMonths(1);
        }
        var latest=rows.OrderByDescending(r=>r.Request.CreatedAt).ThenByDescending(r=>r.Request.Id).Take(10)
            .Select(r=>new DashboardHistory(r.Request.Id,r.Request.RequestNo,r.Request.Title,r.Request.Status,r.Request.CreatedAt,r.Request.NeededDate)).ToList();
        return new(division,name,from,to,kinds.Count,report.Total,report.PerJenis,status,months,kinds,latest,
            division=="nat" ? "Ringkasan NAT saat ini mencakup master jenis layanan dan riwayat tiket. Data penggunaan alat, jam operasi, dan biaya belum tersedia." : null);
    }
}
