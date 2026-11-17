using System.Security.Claims;
using AuthUtility.Api.Controllers;
using AuthUtility.Api.Sensus;
using AuthUtility.Application.DTOs.Groups;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using AuthUtility.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Tests;

public class SensusWebMenuSyncTests
{
    [Fact]
    public async Task RealMenusAppearInManagementAndAccessAndExistingRightsArePreserved()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Groups.AddRange(new UtlGroup {Id=1,Code="ADMIN",IsSuperAdmin=true}, new UtlGroup {Id=2,Code="OFFICE"});
        db.Users.Add(new() {Id=1,GroupId=1,UserId="admin"});
        db.Menus.AddRange(new UtlMenu {Id=1,Code="LAYANAN",MenuType="GROUP"},new UtlMenu {Id=2,Code="LYN_RNA",MenuType="GROUP"},
            new UtlMenu {Id=3,Code="SNS_MOBILE",Name="Input Sensus (Android)"},new UtlMenu {Id=4,Code="SNS_MONITORING",Name="Monitoring"},
            new UtlMenu {Id=5,Code="SNS_APPROVAL",Name="Approval Sensus"},new UtlMenu {Id=6,Code="SNS_ANALISIS",Name="Analisis"},
            new UtlMenu {Id=7,Code="LYN_RNA_HPT_ULAT_API",Name="Sensus Ulat Api"},new UtlMenu {Id=8,Code="QC_MONITORING",Name="Unchanged QC"});
        db.GroupMenus.AddRange(new UtlGroupMenu {GroupId=2,MenuId=3,CanView=true,CanCreate=true},
            new UtlGroupMenu {GroupId=2,MenuId=4,CanView=true,CanRead=true},new UtlGroupMenu {GroupId=2,MenuId=6,CanExport=true});
        db.SnsJenis.Add(new() {Code="ULAT_API",MenuCode="LYN_RNA_HPT_ULAT_API"}); await db.SaveChangesAsync();
        Assert.True(await SensusWebMenuSync.EnsureAsync(db));
        var dashboard=db.Menus.Single(m=>m.Code=="SNS_DASHBOARD");
        var actual=db.Menus.Where(m=>m.Active && m.ParentId==2).OrderBy(m=>m.SortOrder).ToList();
        Assert.Equal(new[]{"Dashboard","Input Sensus","Approval","Monitoring & Analisis"},actual.Select(m=>m.Name));
        Assert.Equal(1,db.Menus.Single(m=>m.Id==2).ParentId);
        Assert.Equal("Unchanged QC",db.Menus.Single(m=>m.Id==8).Name);
        Assert.False(db.Menus.Single(m=>m.Id==7).Active); Assert.Single(db.SnsJenis);
        Assert.True(db.GroupMenus.Single(r=>r.GroupId==2 && r.MenuId==dashboard.Id).CanRead);
        Assert.True(db.GroupMenus.Single(r=>r.GroupId==2 && r.MenuId==4).CanExport);
        Assert.True(db.GroupMenus.Single(r=>r.GroupId==2 && r.MenuId==3).CanCreate);
        Assert.Contains(db.GroupMenus,r=>r.MenuId==6); // keep old permission history
        var context=new ControllerContext {HttpContext=new DefaultHttpContext {User=new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"1")},"test"))}};
        var service=new PermissionService(db);
        var management=await new MenusController(db,service){ControllerContext=context}.GetAll();
        var all=Assert.IsType<List<AuthUtility.Application.DTOs.Menus.MenuDto>>(Assert.IsType<OkObjectResult>(management.Result).Value);
        var access=Assert.IsAssignableFrom<IEnumerable<GroupMenuAccessDto>>(Assert.IsType<OkObjectResult>(await new GroupsController(db,service){ControllerContext=context}.GetMenuAccess(2)).Value).ToList();
        foreach(var menu in actual) { Assert.Contains(all,m=>m.Id==menu.Id && m.Name==menu.Name); Assert.Contains(access,m=>m.MenuId==menu.Id && m.Name==menu.Name); }
        Assert.False(access.Single(m=>m.Code=="SNS_APPROVAL").CanApprove);
        var count=db.Menus.Count(); var rightCount=db.GroupMenus.Count();
        Assert.False(await SensusWebMenuSync.EnsureAsync(db));
        Assert.Equal(count,db.Menus.Count()); Assert.Equal(rightCount,db.GroupMenus.Count());
        dashboard.Name="Dashboard kantor";dashboard.Active=false;await db.SaveChangesAsync();
        await SensusWebMenuSync.EnsureAsync(db);
        Assert.Equal("Dashboard kantor",dashboard.Name);Assert.False(dashboard.Active);
    }
}
