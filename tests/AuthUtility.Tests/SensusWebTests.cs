using System.Security.Claims;
using AuthUtility.Api.Controllers;
using AuthUtility.Api.Sensus;
using AuthUtility.Application.DTOs.Security;
using AuthUtility.Application.DTOs.Sensus;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuthUtility.Tests;

public class SensusWebTests
{
    [Fact]
    public async Task PhotoUploadReusesExistingTableAndSafeRelativePathsAndCanBeDeleted()
    {
        await using var db = await Database();
        var environment = new TestEnvironment();
        try
        {
            var controller = Controller(db);
            var result = Assert.IsType<OkObjectResult>(await controller.UploadFoto(7, Png(), "Daun", environment, default));
            var foto = Assert.IsType<SnsFotoDto>(result.Value);
            Assert.StartsWith("/uploads/sensus/", foto.FileUrl);
            Assert.Equal(7, Assert.Single(db.SnsFotos).HeaderId);
            var path = SensusPhotoPolicy.OwnedPath(SensusPhotoPolicy.Root(environment), 7, foto.FileUrl);
            Assert.True(File.Exists(path));
            Assert.IsType<NoContentResult>(await controller.DeleteFoto(7, foto.Id, environment, default));
            Assert.Empty(db.SnsFotos);
            Assert.False(File.Exists(path));
            Assert.Equal(SnsStatus.Submitted, db.SnsHeaders.Single().Status);
        }
        finally { environment.Cleanup(); }
    }

    [Fact]
    public async Task SixthPhotoIsRejectedWithoutChangingSavedSensus()
    {
        await using var db = await Database();
        db.SnsFotos.AddRange(Enumerable.Range(0, 5).Select(index => new SnsFoto { HeaderId = 7, FileUrl = $"existing-{index}.png" }));
        await db.SaveChangesAsync();
        var environment = new TestEnvironment();
        try
        {
            Assert.IsType<BadRequestObjectResult>(await Controller(db).UploadFoto(7, Png(), null, environment, default));
            Assert.Equal(5, db.SnsFotos.Count());
            Assert.Equal(SnsStatus.Submitted, db.SnsHeaders.Single().Status);
        }
        finally {environment.Cleanup();}
    }

    [Theory]
    [InlineData(2, true, "SUBMITTED")]
    [InlineData(1, false, "SUBMITTED")]
    [InlineData(1, true, "APPROVED")]
    [InlineData(1, true, "REJECTED")]
    public async Task PhotoWritesRequireOwnerAndSubmitPermissionAndEditableStatus(int userId, bool permission, string status)
    {
        await using var db = await Database();
        db.SnsHeaders.Single().Status = status;
        db.SnsFotos.Add(new SnsFoto { Id = 10, HeaderId = 7, FileUrl = "/uploads/sensus/existing.png" });
        await db.SaveChangesAsync();
        var environment = new TestEnvironment();
        try
        {
            var controller = Controller(db, userId, permission);
            Assert.IsType<ForbidResult>(await controller.UploadFoto(7, Png(), null, environment, default));
            Assert.IsType<ForbidResult>(await controller.DeleteFoto(7, 10, environment, default));
            Assert.Single(db.SnsFotos);
        }
        finally {environment.Cleanup();}
    }

    [Fact]
    public async Task ApprovalDoesNotRequirePhotosAndOwnerCanReadOwnWebSubmission()
    {
        await using var db = await Database();
        var controller = Controller(db);
        var detail = Assert.IsType<OkObjectResult>(await controller.GetDetail(7));
        var dto = Assert.IsType<SnsDetailDto>(detail.Value);
        Assert.True(dto.CanEditPhotos);
        Assert.NotNull(dto.ClientUuid);
        Assert.Empty(dto.Fotos);
        Assert.IsType<OkObjectResult>(await controller.RunAction(7, new() { Action = "APPROVE" }));
        Assert.Equal(SnsStatus.Approved, db.SnsHeaders.Single().Status);
        Assert.Empty(db.SnsFotos);
    }

    [Fact]
    public void DashboardUsesApprovedExistingEvaluationAndLatestPersilAndMonthlyCounts()
    {
        var monitoring = new SnsMonitoringDto(3, 1, 2, 0, 0, new(), new(), new(), new(), "KLP", new(), null);
        var headers = new[] {
            new SnsHeader {Id=1,JenisId=1,PersilSumber="KLP",PersilId="A",Status="APPROVED",TanggalSensus=new(2026,10,1),JumlahPokokSampel=10,JumlahPokokTerserang=5},
            new SnsHeader {Id=2,JenisId=1,PersilSumber="KLP",PersilId="A",Status="APPROVED",TanggalSensus=new(2026,11,1),JumlahPokokSampel=10,JumlahPokokTerserang=1},
            new SnsHeader {Id=3,JenisId=1,PersilSumber="KLP",PersilId="B",Status="SUBMITTED",TanggalSensus=new(2026,11,1)}
        };
        var evaluation = new Dictionary<long,(string Tingkat, decimal? Rata)> { [1]=("BERAT",10),[2]=("RINGAN",1) };
        var dashboard = SensusDashboardCalculator.Build(monitoring,headers,evaluation);
        Assert.Equal(3,dashboard.Total);
        Assert.Equal(2,dashboard.PerTingkat.Values.Sum());
        Assert.Equal(1,dashboard.Berat);
        Assert.Equal(new[] {1,2},dashboard.TrenBulanan.Select(x=>x.Total));
        Assert.Equal("RINGAN",Assert.Single(dashboard.TopPersil).Tingkat);
        Assert.Equal(10,Assert.Single(dashboard.TopPersil).PersenTerserang);
    }

    private static IFormFile Png() => new FormFile(new MemoryStream(new byte[]{137,80,78,71,13,10,26,10}),0,8,"file","../../client.png")
        {Headers=new HeaderDictionary(),ContentType="image/png"};

    private static async Task<AppDbContext> Database()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.Add(new UtlUser {Id=1,UserId="petugas",Name="Petugas",GroupId=1,PasswordHash="test"});
        db.SnsJenis.Add(new SnsJenis {Id=1,Code="TEST_MASTER",Name="Master-driven type"});
        db.SnsHeaders.Add(new SnsHeader {Id=7,JenisId=1,ClientUuid=Guid.NewGuid(),PetugasUserId=1,Status=SnsStatus.Submitted,TanggalSensus=new(2026,11,14),PersilSumber="KLP",PersilId="7",PersilKode="A"});
        await db.SaveChangesAsync();
        return db;
    }

    private static SensusController Controller(AppDbContext db,int userId=1,bool allow=true)
    {
        var reader = new PersilReader(new MyPADDbContext(new DbContextOptionsBuilder<MyPADDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options),
            new RSUPPKBDbContext(new DbContextOptionsBuilder<RSUPPKBDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options));
        return new(db,new TestPermissions(allow),reader,NullLogger<SensusController>.Instance) {
            ControllerContext=new(){HttpContext=new DefaultHttpContext {User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,userId.ToString())},"test"))}}
        };
    }

    private sealed class TestPermissions(bool allow) : IPermissionService
    {
        public Task<bool> IsSuperAdminAsync(ClaimsPrincipal user)=>Task.FromResult(false);
        public Task<bool> HasPermissionAsync(ClaimsPrincipal user,string code,MenuPermission permission)=>Task.FromResult(allow);
        public Task<CurrentMenuPermissionDto?> GetPermissionsAsync(ClaimsPrincipal user,string code)=>Task.FromResult<CurrentMenuPermissionDto?>(allow?new(){CanRead=true,CanCreate=true}:null);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ContentRootPath {get;set;}=Path.Combine(Path.GetTempPath(),"sensus-test-"+Guid.NewGuid().ToString("N"));
        public string WebRootPath {get;set;}="";
        public string ApplicationName {get;set;}="test";
        public string EnvironmentName {get;set;}="test";
        public IFileProvider WebRootFileProvider {get;set;}=new NullFileProvider();
        public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();
        public void Cleanup()
        {
            var path=Path.GetFullPath(ContentRootPath);
            if(!path.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(path).StartsWith("sensus-test-")) throw new InvalidOperationException("Unsafe test cleanup path");
            if(Directory.Exists(path))Directory.Delete(path,true);
        }
    }
}
