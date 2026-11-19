using System.Security.Claims;
using AuthUtility.Api.Controllers;
using AuthUtility.Api.Sensus;
using AuthUtility.Application.DTOs.Security;
using AuthUtility.Application.DTOs.Sensus;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuthUtility.Tests;

public class SensusParcelAccessTests
{
    [Fact]
    public async Task CombinedApprovalRequiresAllChildPermissionsAndLeavesStatusWhenDenied()
    {
        await using var db = await Database();
        var controller = Controller(db, new Permissions("HPT_A"));
        Assert.IsType<ObjectResult>(await controller.RunAction(1, new() { Action = "APPROVE" }));
        Assert.Equal(SnsStatus.Submitted, (await db.SnsHeaders.SingleAsync()).Status);
        controller = Controller(db, new Permissions("HPT_A", "HPT_B"));
        Assert.IsType<OkObjectResult>(await controller.RunAction(1, new() { Action = "APPROVE" }));
        Assert.Equal(SnsStatus.Approved, (await db.SnsHeaders.SingleAsync()).Status);
        Assert.Single(db.SnsLogs);
    }

    [Fact]
    public async Task PartialHptReadHidesOtherChildrenAndFilterUsesChildCount()
    {
        await using var db = await Database();
        var controller = Controller(db, new Permissions("HPT_A"));
        var detailResult = Assert.IsType<OkObjectResult>(await controller.GetDetail(1));
        var detail = Assert.IsType<SnsDetailDto>(detailResult.Value);
        Assert.Equal(10, Assert.Single(detail.Summary.HptDetails!).HptId);
        Assert.False(detail.CanApprove);
        var result = Assert.IsType<OkObjectResult>(await controller.GetList(null, 10, null, null, null, null, null, null, null, null));
        var row = Assert.Single(Assert.IsType<List<SnsListItemDto>>(result.Value));
        Assert.Equal(3, row.JumlahPokokTerserang);
        Assert.Equal(10, Assert.Single(row.HptDetails!).HptId);
    }

    [Fact]
    public async Task AnalysisUsesRequestedHptAndExcludesUnapprovedHeader()
    {
        await using var db = await Database();
        var controller = Controller(db, new Permissions("HPT_A", "HPT_B"));
        var empty = Assert.IsType<SnsAnalisisDto>(Assert.IsType<OkObjectResult>(await controller.GetAnalisis(20,null,new(2026,11,1),new(2026,11,30))).Value);
        Assert.Equal(0, empty.JumlahSensus);
        (await db.SnsHeaders.SingleAsync()).Status = SnsStatus.Approved;
        await db.SaveChangesAsync();
        var result = Assert.IsType<SnsAnalisisDto>(Assert.IsType<OkObjectResult>(await controller.GetAnalisis(20,null,new(2026,11,1),new(2026,11,30))).Value);
        Assert.Equal(1, result.JumlahSensus);
        Assert.Equal(7, result.PersenTerserang);
        Assert.Equal(7, Assert.Single(result.PerPersil).TotalPokokTerserang);
    }

    [Fact]
    public async Task ScopedSeverityCannotComeFromHiddenHptParameters()
    {
        await using var db=await Database();
        db.SnsDetails.AddRange(new SnsDetail{HeaderId=1,ParameterId=101,Parameter=new(){Id=101,JenisId=10,Code="COUNT_A",IsIndikator=true,AmbangBerat=5},NilaiAngka=0},
            new SnsDetail{HeaderId=1,ParameterId=102,Parameter=new(){Id=102,JenisId=20,Code="COUNT_B",IsIndikator=true,AmbangBerat=5},NilaiAngka=10});
        await db.SaveChangesAsync();
        var controller=Controller(db,new Permissions("HPT_A"));
        var detail=Assert.IsType<SnsDetailDto>(Assert.IsType<OkObjectResult>(await controller.GetDetail(1)).Value);
        Assert.Equal(SnsTingkat.Nihil,detail.Summary.Tingkat);
        var rows=Assert.IsType<List<SnsListItemDto>>(Assert.IsType<OkObjectResult>(await controller.GetList(null,10,null,null,null,null,null,null,null,null)).Value);
        Assert.Equal(SnsTingkat.Nihil,Assert.Single(rows).Tingkat);
    }

    [Fact]
    public void Form1DashboardSeverityUsesChildRulesWithoutInjectedParameterLevels()
    {
        var kind=new SnsJenis{Id=10,Rules=new List<SnsHptRule>{new(){ParameterCode="PERSEN_TERSERANG",Operator=">=",ThresholdValue=5}}};
        var header=new SnsHeader{Id=1,Status="APPROVED",JumlahPokokSampel=100,HptDetails=new List<SnsHptDetail>{new(){HptId=10,Hpt=kind,JumlahSerangan=6}}};
        var monitoring=new SnsMonitoringDto(1,0,1,0,0,new(),new(),new(),new(),"KLP",new(),null);
        var dashboard=SensusDashboardCalculator.Build(monitoring,new[]{header},new Dictionary<long,(string Tingkat,decimal? Rata)>());
        Assert.Equal(1,dashboard.Berat);
        Assert.Equal(1,dashboard.PerTingkat[SnsTingkat.Berat]);
    }

    [Fact]
    public void ChildOutputSeparatesSavedEvaluationFromCurrentMasterRecommendation()
    {
        var kind=new SnsJenis{Id=10,Rules=new List<SnsHptRule>{new(){ParameterCode="PERSEN_TERSERANG",Operator=">=",ThresholdValue=5,Message="Current master"}}};
        var saved=new SnsEvaluationDto("MASTER_RULE",false,6,"%","Previous rule","Previous monitoring");
        var header=new SnsHeader{JumlahPokokSampel=100,HptDetails=new List<SnsHptDetail>{new(){HptId=10,Hpt=kind,JumlahSerangan=6,HasilEvaluasi=System.Text.Json.JsonSerializer.Serialize(saved)}}};
        var child=Assert.Single(SensusParcelHpt.Dtos(header));
        Assert.False(child.HasilEvaluasi.PerluPengendalian);
        Assert.True(child.CurrentEvaluation!.PerluPengendalian);
        Assert.Equal("Current master",child.CurrentEvaluation.Message);
    }

    private static async Task<AppDbContext> Database()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.Add(new() { Id=1, UserId="field", Name="Field", PasswordHash="test", GroupId=1 });
        var a = new SnsJenis { Id=10, Code="A",Name="A",MenuCode="HPT_A",ReportGroup="HAMA" };
        var b = new SnsJenis { Id=20, Code="B",Name="B",MenuCode="HPT_B",ReportGroup="PENYAKIT" };
        db.SnsJenis.AddRange(a,b);
        db.SnsHeaders.Add(new() { Id=1, JenisId=null, PetugasUserId=1, ClientUuid=Guid.NewGuid(), PersilSumber="KLP",PersilId="P1",PersilKode="P1",TanggalSensus=new(2026,11,18),JumlahPokokSampel=100,
            HptDetails=new List<SnsHptDetail> { new(){HptId=10,Hpt=a,JumlahSerangan=3},new(){HptId=20,Hpt=b,JumlahSerangan=7} } });
        await db.SaveChangesAsync();
        return db;
    }
    private static SensusController Controller(AppDbContext db, IPermissionService permissions)
    {
        var reader=new PersilReader(new MyPADDbContext(new DbContextOptionsBuilder<MyPADDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options),new RSUPPKBDbContext(new DbContextOptionsBuilder<RSUPPKBDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options));
        return new(db,permissions,reader,NullLogger<SensusController>.Instance) { ControllerContext=new(){HttpContext=new DefaultHttpContext {User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"99")},"test"))}} };
    }
    private sealed class Permissions(params string[] menus) : IPermissionService
    {
        public Task<bool> IsSuperAdminAsync(ClaimsPrincipal user)=>Task.FromResult(false);
        public Task<bool> HasPermissionAsync(ClaimsPrincipal user,string code,MenuPermission permission)=>Task.FromResult(menus.Contains(code));
        public Task<CurrentMenuPermissionDto?> GetPermissionsAsync(ClaimsPrincipal user,string code)=>Task.FromResult<CurrentMenuPermissionDto?>(menus.Contains(code)?new(){CanRead=true,CanView=true,CanApprove=true}:null);
    }
}
