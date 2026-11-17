using System.Security.Claims;
using AuthUtility.Api.Controllers;
using AuthUtility.Application.DTOs.Layanan;
using AuthUtility.Application.DTOs.Menus;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using AuthUtility.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AuthUtility.Tests;

public class LayananRequestPolicyTests
{
    private static ControllerContext Context() => new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "test")) } };
    private static async Task<AppDbContext> Database(string division)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        db.Groups.Add(new() { Id=1, Code="ADMIN", IsSuperAdmin=true, AllDepartments=true });
        db.Users.Add(new() { Id=1, GroupId=1, UserId="test", Name="Tester" });
        db.Menus.AddRange(new UtlMenu { Id=1,Code="LAYANAN",MenuType="GROUP" },
            new UtlMenu { Id=2,ParentId=1,Code=division,Name="Division",MenuType="GROUP" },
            new UtlMenu { Id=3,ParentId=2,Code=division+"_SUB",Name="Service",Route="/layanan/test" });
        db.LynJenisPermintaan.Add(new() { Id=1,DivisionMenuId=2,Code="JENIS_TEST",Name="Test type" });
        db.LynRequests.Add(new() { Id=100,MenuId=3,RequesterUserId=1,Title="Historical request" });
        await db.SaveChangesAsync(); return db;
    }

    [Theory]
    [InlineData("LYN_RNA",false)] [InlineData("LYN_RNA",true)]
    [InlineData("LYN_WAD",false)] [InlineData("LYN_WAD",true)]
    [InlineData("LYN_QC",false)] [InlineData("LYN_QC",true)]
    [InlineData("LYN_NAT",false)] [InlineData("LYN_NAT",true)]
    public async Task NonGisRequestsRejectedForLegacyMenuAndMasterJenisWithoutDeletingHistory(string division,bool byJenis)
    {
        await using var db = await Database(division);
        var controller = new LayananController(db) { ControllerContext=Context() };
        var request = new CreateLynRequest { MenuCode=division+"_SUB",JenisId=byJenis?1:null };
        var result = Assert.IsType<BadRequestObjectResult>(await controller.CreateRequest(request));
        Assert.Contains("hanya tersedia untuk GIS",System.Text.Json.JsonSerializer.Serialize(result.Value));
        Assert.Equal("Historical request",Assert.Single(db.LynRequests).Title);
        var services=Assert.IsAssignableFrom<IEnumerable<LynServiceDto>>(Assert.IsType<OkObjectResult>(await controller.GetServices()).Value);
        Assert.All(services,s=>Assert.False(s.Permissions.CanCreate));
    }

    [Fact]
    public async Task GisStillCreatesRequestsWithExistingWorkflow()
    {
        await using var db = await Database("LYN_GIS");
        var controller = new LayananController(db) { ControllerContext=Context() };
        Assert.IsType<OkObjectResult>(await controller.CreateRequest(new() { JenisId=1,RequesterName="Petugas",RequesterPosition="Staff",
            RequesterDepartment="GIS",Companies=new(){"RSUP"},RequesterPhone="123",RequesterEmail="test@example.com",Description="Pemetaan",Location="Persil 1",NeededDate=new DateOnly(2026,12,1) }));
        Assert.Equal(2,db.LynRequests.Count());
        Assert.Equal(LynStatus.Baru,db.LynRequests.Single(r=>r.Id!=100).Status);
    }

    [Fact]
    public async Task HptLegacyIsExcludedFromNavigationEvenWhenPlacedAtRootAndMasterRemains()
    {
        await using var db = await Database("LYN_GIS");
        db.Menus.AddRange(new UtlMenu { Id=10,Code="LYN_RNA_HPT_ULAT_API",Name="Sensus Ulat Api" },
            new UtlMenu { Id=11,Code="CUSTOM_HPT_MENU",Name="HPT legacy" });
        db.SnsJenis.Add(new() { Id=1,Code="ULAT_API",MenuCode="CUSTOM_HPT_MENU" }); await db.SaveChangesAsync();
        var controller = new MenusController(db,new PermissionService(db)) { ControllerContext=Context() };
        var menus=Assert.IsType<List<NavigationMenuDto>>(Assert.IsType<OkObjectResult>(await controller.GetNavigation()).Value);
        Assert.DoesNotContain(menus,m=>m.Id is 10 or 11);
        Assert.Single(db.SnsJenis); Assert.True(db.Menus.Single(m=>m.Id==11).Active);
    }
}
