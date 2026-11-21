using AuthUtility.Domain.Entities;

namespace AuthUtility.Api.Dashboards;

public record QcDashboardInspection(long Id,string? InspectionNo,string FormCode,string FormName,DateOnly InspectionDate,string? WilayahKode,string Status);
public record QcDashboardFinding(long InspectionId,string Status,string Severity,DateOnly? TargetDate);
public record QcDashboardGroup(string Key,string Label,int Total);
public record QcDashboardRecent(QcDashboardInspection Inspection,int OpenFindingCount,int OverdueFindingCount);
public record QcDashboardDto(DateOnly DateFrom,DateOnly DateTo,int TotalInspections,int PendingApproval,int Approved,int Revision,
    int TotalFindings,int OpenFindings,int OverdueFindings,int CriticalOpenFindings,List<QcDashboardGroup> PerStatus,List<QcDashboardGroup> PerForm,
    List<QcDashboardGroup> PerWilayah,List<QcDashboardGroup> PerSeverity,List<QcDashboardGroup> PerFindingStatus,List<DashboardMonth> PerBulan,List<QcDashboardRecent> Latest);

public static class QcDashboardCalculator
{
    public static QcDashboardDto Build(List<QcDashboardInspection> inspections,List<QcDashboardFinding> findings,DateOnly from,DateOnly to,DateOnly today)
    {
        bool Open(QcDashboardFinding f)=>QcFindingStatus.Active.Contains(f.Status);
        bool Overdue(QcDashboardFinding f)=>Open(f) && f.TargetDate<today;
        var active=findings.Where(Open).ToList();
        var status=inspections.GroupBy(i=>i.Status).Select(g=>new QcDashboardGroup(g.Key,g.Key,g.Count())).OrderBy(g=>g.Key).ToList();
        var forms=inspections.GroupBy(i=>new{i.FormCode,i.FormName}).Select(g=>new QcDashboardGroup(g.Key.FormCode,g.Key.FormName,g.Count())).OrderByDescending(g=>g.Total).ThenBy(g=>g.Label).ToList();
        var wilayah=inspections.GroupBy(i=>string.IsNullOrWhiteSpace(i.WilayahKode)?"Belum diisi":i.WilayahKode)
            .Select(g=>new QcDashboardGroup(g.Key!,g.Key!,g.Count())).OrderByDescending(g=>g.Total).ThenBy(g=>g.Label).ToList();
        var severity=active.GroupBy(f=>f.Severity).Select(g=>new QcDashboardGroup(g.Key,g.Key,g.Count())).OrderBy(g=>g.Key).ToList();
        var findingStatus=findings.GroupBy(f=>f.Status).Select(g=>new QcDashboardGroup(g.Key,g.Key,g.Count())).OrderBy(g=>g.Key).ToList();
        var counts=inspections.GroupBy(i=>i.InspectionDate.ToString("yyyy-MM")).ToDictionary(g=>g.Key,g=>g.Count());
        var months=new List<DashboardMonth>();
        for(var month=new DateOnly(from.Year,from.Month,1);month<=to;)
        {
            var key=month.ToString("yyyy-MM");months.Add(new(key,counts.GetValueOrDefault(key)));
            if(month.Year==to.Year && month.Month==to.Month)break;
            month=month.AddMonths(1);
        }
        var findingCounts=findings.GroupBy(f=>f.InspectionId).ToDictionary(g=>g.Key,g=>(Open:g.Count(Open),Overdue:g.Count(Overdue)));
        var recent=inspections.OrderByDescending(i=>i.InspectionDate).ThenByDescending(i=>i.Id).Take(10)
            .Select(i=>new QcDashboardRecent(i,findingCounts.GetValueOrDefault(i.Id).Open,findingCounts.GetValueOrDefault(i.Id).Overdue)).ToList();
        return new(from,to,inspections.Count,inspections.Count(i=>QcInspectionStatus.PendingApproval.Contains(i.Status)),inspections.Count(i=>i.Status==QcInspectionStatus.Approved),
            inspections.Count(i=>i.Status==QcInspectionStatus.Revision),findings.Count,active.Count,findings.Count(Overdue),active.Count(f=>f.Severity==QcFindingSeverity.Critical),
            status,forms,wilayah,severity,findingStatus,months,recent);
    }
}
