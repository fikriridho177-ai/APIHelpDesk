using System.Security.Claims;
using AuthUtility.Api.Controllers;
using AuthUtility.Api.Dashboards;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Tests;

public class QcDashboardTests
{
    private static readonly DateOnly Day=new(2026,11,16);
    private static async Task<AppDbContext> Database()
    {
        var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Groups.Add(new(){Id=1,Code="OFFICE"});db.Users.Add(new(){Id=1,GroupId=1,UserId="owner"});
        db.Menus.AddRange(new UtlMenu{Id=1,Code="QC_DASHBOARD"},new UtlMenu{Id=2,Code="QC_INSPEKSI"});
        db.GroupMenus.AddRange(new UtlGroupMenu{GroupId=1,MenuId=1,CanRead=true,CanView=true},new UtlGroupMenu{GroupId=1,MenuId=2,CanRead=false,CanView=true});
        db.QcForms.Add(new(){Id=1,Code="QC-TEST",Name="Pemeriksaan",Commodity="KELAPA"});
        db.QcInspections.AddRange(new QcInspection{Id=1,FormId=1,InspectionDate=Day,CreatedBy="owner",Status=QcInspectionStatus.Submitted,WilayahKode="W1"},
            new QcInspection{Id=2,FormId=1,InspectionDate=Day,CreatedBy="other",Status=QcInspectionStatus.Approved,WilayahKode="W2"},
            new QcInspection{Id=3,FormId=1,InspectionDate=Day.AddDays(-1),CreatedBy="owner",Status=QcInspectionStatus.Draft});
        db.QcFindings.AddRange(new QcFinding{Id=1,InspectionId=1,Status=QcFindingStatus.Open,Severity=QcFindingSeverity.Minor,TargetDate=Day.AddDays(-1)},
            new QcFinding{Id=2,InspectionId=1,Status=QcFindingStatus.Verified,Severity=QcFindingSeverity.Critical,TargetDate=Day.AddDays(-1)},
            new QcFinding{Id=3,InspectionId=2,Status=QcFindingStatus.Open,Severity=QcFindingSeverity.Critical});
        await db.SaveChangesAsync();return db;
    }
    private static QcDashboardController Controller(AppDbContext db)=>new(db){ControllerContext=new(){HttpContext=new DefaultHttpContext{
        User=new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"1")},"test"))}}};
    private static async Task<QcDashboardDto> Get(AppDbContext db)=>Assert.IsType<QcDashboardDto>(Assert.IsType<OkObjectResult>(await Controller(db).Get(Day,Day)).Value);

    [Fact]
    public async Task DashboardPreservesOwnerHandlerVisibilityAndExcludesFindingsOutsideSelectedInspections()
    {
        await using var db=await Database();
        var owner=await Get(db);Assert.Equal(1,owner.TotalInspections);Assert.Equal(1,owner.PendingApproval);
        Assert.Equal(2,owner.TotalFindings);Assert.Equal(1,owner.OpenFindings);Assert.Equal(1,owner.OverdueFindings);Assert.Equal(0,owner.CriticalOpenFindings);
        Assert.Equal("MINOR",Assert.Single(owner.PerSeverity).Key);Assert.Equal(1,Assert.Single(owner.Latest).Inspection.Id);
        db.GroupMenus.Single(r=>r.MenuId==2).CanRead=true;await db.SaveChangesAsync();
        var handler=await Get(db);Assert.Equal(2,handler.TotalInspections);Assert.Equal(1,handler.Approved);
        Assert.Equal(3,handler.TotalFindings);Assert.Equal(2,handler.OpenFindings);Assert.Equal(1,handler.CriticalOpenFindings);
    }

    [Fact]
    public async Task AggregateCountsAreNotCappedAtFiveHundred()
    {
        await using var db=await Database();db.GroupMenus.Single(r=>r.MenuId==2).CanRead=true;
        db.QcInspections.AddRange(Enumerable.Range(100,600).Select(id=>new QcInspection{Id=id,FormId=1,InspectionDate=Day,CreatedBy="other"}));
        await db.SaveChangesAsync();var data=await Get(db);
        Assert.Equal(602,data.TotalInspections);Assert.Equal(602,data.PerStatus.Sum(g=>g.Total));Assert.Equal(602,data.PerForm.Sum(g=>g.Total));
        Assert.Equal(602,Assert.Single(data.PerBulan).Total);Assert.Equal(10,data.Latest.Count);
    }

    [Fact]
    public async Task DashboardRequiresItsOwnViewAndReadAndHonorsInactiveMenu()
    {
        await using var db=await Database();var controller=Controller(db);var right=db.GroupMenus.Single(r=>r.MenuId==1);
        right.CanRead=false;await db.SaveChangesAsync();Assert.IsType<ForbidResult>(await controller.Get(Day,Day));
        right.CanRead=true;right.CanView=false;await db.SaveChangesAsync();Assert.IsType<ForbidResult>(await controller.Get(Day,Day));
        right.CanView=true;db.Menus.Single(m=>m.Id==1).Active=false;await db.SaveChangesAsync();Assert.IsType<ForbidResult>(await controller.Get(Day,Day));
    }

    [Fact]
    public async Task EmptyPeriodAndInvalidDatesAreHandledWithoutInventedCounts()
    {
        await using var db=await Database();var controller=Controller(db);
        var empty=Assert.IsType<QcDashboardDto>(Assert.IsType<OkObjectResult>(await controller.Get(Day.AddDays(1),Day.AddDays(1))).Value);
        Assert.Equal(0,empty.TotalInspections);Assert.Equal(0,empty.OpenFindings);Assert.Empty(empty.PerStatus);Assert.Empty(empty.Latest);
        Assert.Equal(0,Assert.Single(empty.PerBulan).Total);
        Assert.IsType<BadRequestObjectResult>(await controller.Get(Day,Day.AddDays(-1)));
        Assert.IsType<BadRequestObjectResult>(await controller.Get(Day.AddDays(-366),Day));
    }

    [Fact]
    public void OverdueAndCriticalCountsRespectVerifiedNullAndTodayTargets()
    {
        var inspections=new List<QcDashboardInspection>{new(1,"QC-1","TEST","Test",Day,null,QcInspectionStatus.Revision)};
        var findings=new List<QcDashboardFinding>{new(1,QcFindingStatus.Done,QcFindingSeverity.Critical,Day.AddDays(-1)),
            new(1,QcFindingStatus.Verified,QcFindingSeverity.Major,Day.AddDays(-1)),new(1,QcFindingStatus.Open,QcFindingSeverity.Critical,Day),
            new(1,QcFindingStatus.InProgress,QcFindingSeverity.Minor,null)};
        var data=QcDashboardCalculator.Build(inspections,findings,Day,Day,Day);
        Assert.Equal(3,data.OpenFindings);Assert.Equal(1,data.OverdueFindings);Assert.Equal(2,data.CriticalOpenFindings);Assert.Equal(1,data.Revision);
        Assert.Equal("Belum diisi",Assert.Single(data.PerWilayah).Label);Assert.Equal(3,data.PerSeverity.Sum(g=>g.Total));
        Assert.Equal(1,Assert.Single(data.Latest).OverdueFindingCount);
    }
}
