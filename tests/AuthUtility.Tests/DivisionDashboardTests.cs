using System.Security.Claims;
using AuthUtility.Api.Controllers;
using AuthUtility.Api.Dashboards;
using AuthUtility.Api.Layanan;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using AuthUtility.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Tests;

public class DivisionDashboardTests
{
    private static ControllerContext Context() => new() {HttpContext=new DefaultHttpContext {User=new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"1")},"test"))}};
    private static async Task<AppDbContext> Database()
    {
        var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Groups.AddRange(new UtlGroup {Id=1,Code="READER"},new UtlGroup {Id=2,Code="OTHER"});
        db.Users.AddRange(new UtlUser {Id=1,GroupId=1,UserId="reader",DeptId="10"},new UtlUser {Id=2,GroupId=2,UserId="other"});
        db.GroupDepartments.Add(new() {GroupId=1,DepartmentId=10});
        db.Menus.AddRange(new UtlMenu {Id=1,Code="LAYANAN",MenuType="GROUP"},new UtlMenu {Id=2,Code="LYN_GIS",Name="GIS",MenuType="GROUP",ParentId=1},
            new UtlMenu {Id=3,Code="LYN_NAT",Name="NAT",MenuType="GROUP",ParentId=1},new UtlMenu {Id=4,Code="LYN_GIS_MAP",ParentId=2},new UtlMenu {Id=5,Code="LYN_NAT_LEGACY",ParentId=3},
            new UtlMenu {Id=6,Code="GIS_DASHBOARD",ParentId=2},new UtlMenu {Id=7,Code="NAT_DASHBOARD",ParentId=3});
        db.GroupMenus.AddRange(Enumerable.Range(2,6).Select(id=>new UtlGroupMenu {GroupId=1,MenuId=id,CanView=true,CanRead=true}));
        db.LynJenisPermintaan.AddRange(new LynJenisPermintaan {Id=1,DivisionMenuId=2,Code="GIS_TYPE",Name="Pemetaan"},
            new LynJenisPermintaan {Id=2,DivisionMenuId=3,Code="NAT_TYPE",Name="Layanan NAT"},new LynJenisPermintaan {Id=3,DivisionMenuId=3,Code="OLD",Name="Inactive",Active=false});
        await db.SaveChangesAsync(); return db;
    }
    private static LayananController Controller(AppDbContext db)=>new(db){ControllerContext=Context()};
    private static async Task<DivisionDashboardDto> Dashboard(AppDbContext db,string division,DateOnly from,DateOnly to)=>
        Assert.IsType<DivisionDashboardDto>(Assert.IsType<OkObjectResult>(await Controller(db).GetDivisionDashboard(division,from,to,new PermissionService(db))).Value);

    [Fact]
    public async Task EndpointsIsolateDivisionWibDateBoundariesAndExistingDepartmentOwnerScope()
    {
        await using var db=await Database();
        var start=new DateTime(2026,11,15,17,0,0,DateTimeKind.Utc);
        LynRequest Row(long id,int menu,string dept,int owner,DateTime date)=>new(){Id=id,MenuId=menu,RequesterDeptId=dept,RequesterUserId=owner,CreatedAt=date,Title="Record",Status=LynStatus.Baru};
        db.LynRequests.AddRange(Row(1,2,"10",2,start),Row(2,4,"20",2,start),Row(3,4,"20",1,start),
            Row(4,3,"10",2,start),Row(5,2,"10",2,start.AddTicks(-1)),Row(6,2,"10",2,start.AddDays(1)));
        await db.SaveChangesAsync();
        var day=new DateOnly(2026,11,16);
        var gis=await Dashboard(db,"gis",day,day);
        Assert.Equal(2,gis.Summary.Total); Assert.Equal(new long[]{3,1},gis.Latest.Select(r=>r.Id));
        Assert.Single(gis.JenisAktif); Assert.Null(gis.AvailabilityNote);
        Assert.Equal(2,Assert.Single(gis.PerBulan).Total);
        var nat=await Dashboard(db,"nat",day,day);
        Assert.Equal(1,nat.Summary.Total); Assert.Equal(4,Assert.Single(nat.Latest).Id);
        Assert.Equal(1,nat.ActiveKinds); Assert.Contains("jam operasi",nat.AvailabilityNote!);
    }

    [Fact]
    public async Task DashboardPermissionAndInactiveMenuAreEnforcedIndependentlyOfDivisionRights()
    {
        await using var db=await Database();var service=new PermissionService(db);var controller=Controller(db);
        var right=db.GroupMenus.Single(r=>r.MenuId==6);right.CanRead=false;await db.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await controller.GetDivisionDashboard("gis",null,null,service));
        right.CanRead=true;right.CanView=false;await db.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await controller.GetDivisionDashboard("gis",null,null,service));
        right.CanView=true;db.Menus.Single(m=>m.Id==6).Active=false;await db.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await controller.GetDivisionDashboard("gis",null,null,service));
        Assert.IsType<NotFoundResult>(await controller.GetDivisionDashboard("rna",null,null,service));
    }

    [Fact]
    public async Task EmptyNatUsesRealMasterAndUnknownOperationalDataRatherThanInventedAssetMetrics()
    {
        await using var db=await Database();
        var data=await Dashboard(db,"nat",new(2026,9,1),new(2026,11,16));
        Assert.Equal(0,data.Summary.Total);Assert.Equal(1,data.ActiveKinds);Assert.Empty(data.Latest);
        Assert.Null(data.Summary.RataHariSelesai);Assert.Null(data.Summary.PersenTepatWaktu);
        Assert.Equal(new[]{"2026-09","2026-10","2026-11"},data.PerBulan.Select(m=>m.Month));
        Assert.All(data.PerBulan,m=>Assert.Equal(0,m.Total));Assert.NotNull(data.AvailabilityNote);
    }

    [Fact]
    public async Task InvalidPeriodsReturnValidationError()
    {
        await using var db=await Database();var controller=Controller(db);var permissions=new PermissionService(db);
        Assert.IsType<BadRequestObjectResult>(await controller.GetDivisionDashboard("gis",new(2026,11,2),new(2026,11,1),permissions));
        Assert.IsType<BadRequestObjectResult>(await controller.GetDivisionDashboard("gis",new(2024,1,1),new(2026,11,1),permissions));
        Assert.IsType<BadRequestObjectResult>(await controller.GetDivisionDashboard("gis",DateOnly.MaxValue,DateOnly.MaxValue,permissions));
    }

    [Fact]
    public void CalculatorReusesExistingStatusDeadlineAndCompletionRules()
    {
        var today=new DateOnly(2026,11,16);var start=new DateTime(2026,11,1,0,0,0,DateTimeKind.Utc);
        var rows=new[]{new ReportRow(new(){Id=1,CreatedAt=start,Status=LynStatus.Selesai,ClosedAt=start.AddDays(2),NeededDate=today},"GIS","Map"),
            new ReportRow(new(){Id=2,CreatedAt=start,Status=LynStatus.Diproses,NeededDate=today.AddDays(-1)},"GIS","Map"),
            new ReportRow(new(){Id=3,CreatedAt=start,Status=LynStatus.Ditolak,NeededDate=today.AddDays(-1)},"GIS","Drone")};
        var data=DivisionDashboardCalculator.Build("gis","GIS",new(2026,11,1),today,rows,new(),today);
        Assert.Equal(3,data.Summary.Total);Assert.Equal(1,data.Summary.Selesai);Assert.Equal(1,data.Summary.Terlambat);
        Assert.Equal(2d,data.Summary.RataHariSelesai);Assert.Equal(100d,data.Summary.PersenTepatWaktu);
        Assert.Equal(2,data.PerJenis.Count);Assert.Equal(3,data.PerStatus.Sum(s=>s.Total));
    }

    [Fact]
    public async Task DashboardMenusPersistWithExistingReadViewOnlyAndPreserveAdminChangesOnRestart()
    {
        await using var db=await Database();
        db.Menus.RemoveRange(db.Menus.Where(m=>m.Id==6 || m.Id==7));
        db.GroupMenus.RemoveRange(db.GroupMenus.Where(r=>r.MenuId==6 || r.MenuId==7));await db.SaveChangesAsync();
        db.GroupMenus.Single(r=>r.MenuId==2).CanCreate=true;await db.SaveChangesAsync();
        await DivisionDashboardMenuSync.EnsureAsync(db);
        var gis=db.Menus.Single(m=>m.Code=="GIS_DASHBOARD");var nat=db.Menus.Single(m=>m.Code=="NAT_DASHBOARD");
        Assert.Equal(2,gis.ParentId);Assert.Equal(3,nat.ParentId);Assert.Equal("/gis/dashboard",gis.Route);Assert.Equal("/nat/dashboard",nat.Route);
        var right=db.GroupMenus.Single(r=>r.MenuId==gis.Id);Assert.True(right.CanRead && right.CanView);Assert.False(right.CanCreate || right.CanApprove);
        var count=db.Menus.Count();var rights=db.GroupMenus.Count();gis.Active=false;gis.Name="Custom dashboard";gis.Route="/custom-dashboard";await db.SaveChangesAsync();
        await DivisionDashboardMenuSync.EnsureAsync(db);
        Assert.Equal(count,db.Menus.Count());Assert.Equal(rights,db.GroupMenus.Count());Assert.False(gis.Active);Assert.Equal("/custom-dashboard",gis.Route);Assert.Equal("Custom dashboard",gis.Name);
    }
}
