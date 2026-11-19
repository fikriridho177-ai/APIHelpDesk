using AuthUtility.Api.Sensus;
using AuthUtility.Application.DTOs.Sensus;
using AuthUtility.Domain.Entities;
using System.Text.Json;
using System.Security.Claims;
using AuthUtility.Api.Controllers;
using AuthUtility.Application.DTOs.Security;
using AuthUtility.Application.Interfaces;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuthUtility.Tests;

public class SensusParcelHptTests
{
    private static SnsJenis Kind(int id, bool active=true) => new() { Id=id, Name=$"HPT {id}", Active=active, Komoditas="KELAPA", ReportGroup="HAMA" };

    [Fact]
    public void OneParcelExpandsToSeveralIndependentHptObservationsWithSharedContext()
    {
        var a=Kind(1);var b=Kind(2);
        var h=new SnsHeader { Id=7, JenisId=null, JumlahPokokSampel=100, JumlahJalur=5, PohonMatiPetir=1, LuasHa=10, Status="APPROVED", PersilId="P-1",
            HptDetails=new List<SnsHptDetail>{new(){HptId=1,Hpt=a,JumlahSerangan=6},new(){HptId=2,Hpt=b,JumlahSerangan=2}},
            Details=new List<SnsDetail>{new(){ParameterId=11,Parameter=new(){Id=11,JenisId=1}},new(){ParameterId=22,Parameter=new(){Id=22,JenisId=2}}}};
        var expanded=SensusParcelHpt.Expand(h).ToList();
        Assert.Equal(2,expanded.Count);Assert.Equal(new[]{6,2},expanded.Select(x=>x.JumlahPokokTerserang));
        Assert.All(expanded,x=>{Assert.Equal(7,x.Id);Assert.Equal(100,x.JumlahPokokSampel);Assert.Equal(5,x.JumlahJalur);Assert.Equal(1,x.PohonMatiPetir);Assert.Equal(10,x.LuasHa);Assert.Single(x.Details);Assert.Equal(x.JenisId,x.Details.Single().Parameter!.JenisId);});
        Assert.Null(h.JenisId);Assert.Equal(2,h.Details.Count);
        Assert.Equal(new decimal?[]{6,2},SensusParcelHpt.Dtos(h).Select(x=>x.Persentase));
    }

    [Fact]
    public void MasterChangeRequiresReloadAndZeroDoesNotMeanMissing()
    {
        var kinds=new[]{Kind(1),Kind(2)};
        Assert.NotNull(SensusParcelHpt.ValidateInputs(new[]{new SnsHptInput{HptId=1,JumlahSerangan=0}},kinds,100));
        Assert.Null(SensusParcelHpt.ValidateInputs(new[]{new SnsHptInput{HptId=1,JumlahSerangan=0},new SnsHptInput{HptId=2,JumlahSerangan=100}},kinds,100));
        Assert.NotNull(SensusParcelHpt.ValidateInputs(new[]{new SnsHptInput{HptId=1,JumlahSerangan=101},new SnsHptInput{HptId=2}},kinds,100));
        Assert.NotNull(SensusParcelHpt.ValidateInputs(new[]{new SnsHptInput{HptId=1},new SnsHptInput{HptId=1}},kinds,100));
        Assert.NotNull(SensusParcelHpt.ValidateInputs(Array.Empty<SnsHptInput>(),kinds,0));
    }

    [Fact]
    public void RevisionRetainsInactiveHistoricalHptWhileNewTransactionsRejectIt()
    {
        var kinds=new[]{Kind(1),Kind(2,false)};var inputs=new[]{new SnsHptInput{HptId=1},new SnsHptInput{HptId=2}};
        Assert.NotNull(SensusParcelHpt.ValidateInputs(inputs,kinds,100));
        Assert.Null(SensusParcelHpt.ValidateInputs(inputs,kinds,100,new[]{2}));
        Assert.NotNull(SensusParcelHpt.ValidateInputs(new[]{new SnsHptInput{HptId=1}},kinds,100,new[]{2}));
    }

    [Fact]
    public void Form2UsesMetadataAndRetainsInactiveHistoricalObservation()
    {
        var general=Kind(1);var special=Kind(2);special.Form2Enabled=true;var retired=Kind(3,false);
        var kinds=new[]{general,special,retired};
        Assert.Null(SensusParcelHpt.ValidateInputs(new[]{new SnsHptInput{HptId=2}},kinds,100,form:"FORM2"));
        Assert.NotNull(SensusParcelHpt.ValidateInputs(new[]{new SnsHptInput{HptId=1},new SnsHptInput{HptId=2}},kinds,100,form:"FORM2"));
        Assert.Null(SensusParcelHpt.ValidateInputs(new[]{new SnsHptInput{HptId=2},new SnsHptInput{HptId=3}},kinds,100,new[]{3},"FORM2"));
    }

    [Fact]
    public void LegacyZeroSamplesAndMissingSnapshotAreRepresentedWithoutInventedHistory()
    {
        var h=new SnsHeader{JenisId=1,Jenis=Kind(1),JumlahPokokSampel=0};
        Assert.Same(h,Assert.Single(SensusParcelHpt.Expand(h)));
        var dto=Assert.Single(SensusParcelHpt.Dtos(h));Assert.Null(dto.Persentase);Assert.Equal("NONE",dto.HasilEvaluasi.EvaluationType);
    }

    [Fact]
    public void SchemaBackfillsOnlyLegacyHeadersWithoutChangingApprovalOrPhotos()
    {
        using var stream=typeof(SensusBusinessSetup).Assembly.GetManifestResourceStream("Sensus.BusinessSchema.sql")!;
        using var reader=new StreamReader(stream);var sql=reader.ReadToEnd();
        Assert.Contains("CREATE TABLE IF NOT EXISTS \"TrnSnsHptDetail\"",sql);
        Assert.Contains("ON CONFLICT (\"HeaderId\",\"HptId\") DO NOTHING",sql);
        Assert.Contains("ALTER COLUMN \"JenisId\" DROP NOT NULL",sql);
        Assert.DoesNotContain("UPDATE \"TrnSnsHeader\"",sql);
        Assert.DoesNotContain("DELETE FROM",sql);
    }

    [Fact]
    public void MissingAttackInputCannotSilentlyDeserializeAsZero()
    {
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<SnsHptInput>("{\"HptId\":1}"));
        var value=JsonSerializer.Deserialize<SnsHptInput>("{\"HptId\":1,\"JumlahSerangan\":0}");
        Assert.Equal(0,value!.JumlahSerangan);
    }

    [Fact]
    public async Task ResubmittingSavedParcelUuidReturnsExistingHeaderWithoutDuplicatingChildren()
    {
        await using var db=Database();var uuid=Guid.NewGuid();
        db.SnsHeaders.Add(new(){Id=9,ClientUuid=uuid,PetugasUserId=1,Status="SUBMITTED",HptDetails=new List<SnsHptDetail>{new(){HptId=1,JumlahSerangan=6},new(){HptId=2,JumlahSerangan=2}}});
        await db.SaveChangesAsync();
        var response=Assert.IsType<OkObjectResult>(await Controller(db).Submit(new(){ClientUuid=uuid}));
        var result=Assert.IsType<SnsMobileSubmitResponse>(response.Value);Assert.True(result.Duplicate);Assert.Equal(9,result.Id);
        Assert.Single(db.SnsHeaders);Assert.Equal(2,await db.SnsHptDetails.CountAsync());
    }

    [Fact]
    public async Task IncompleteParcelSubmissionIsRejectedBeforeReadingExternalPersilDatabase()
    {
        await using var db=Database();db.SnsJenis.AddRange(Kind(1),Kind(2));await db.SaveChangesAsync();
        var result=await Controller(db).Submit(new(){ClientUuid=Guid.NewGuid(),PersilSumber="KLP",PersilId="P-1",JumlahPokokSampel=100,
            HptDetails=new(){new(){HptId=1,JumlahSerangan=0}},Business=new(){JumlahJalur=5}});
        Assert.IsType<BadRequestObjectResult>(result);Assert.Empty(db.SnsHeaders);Assert.Empty(db.SnsHptDetails);
    }

    [Fact]
    public async Task OldClientCannotEraseOtherHptWhenRevisingCombinedParcel()
    {
        await using var db=Database();db.SnsJenis.AddRange(Kind(1),Kind(2));var uuid=Guid.NewGuid();
        db.SnsHeaders.Add(new(){ClientUuid=uuid,PetugasUserId=1,Status="REVISI",HptDetails=new List<SnsHptDetail>{new(){HptId=1,JumlahSerangan=6},new(){HptId=2,JumlahSerangan=2}}});await db.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await Controller(db).Submit(new(){ClientUuid=uuid,PersilSumber="KLP",PersilId="P-1",JenisId=1,JumlahPokokSampel=100}));
        Assert.Equal("REVISI",db.SnsHeaders.Single().Status);Assert.Equal(2,await db.SnsHptDetails.CountAsync());
    }

    private static AppDbContext Database()=>new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static SensusMobileController Controller(AppDbContext db)
    {
        var reader=new PersilReader(new MyPADDbContext(new DbContextOptionsBuilder<MyPADDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options),new RSUPPKBDbContext(new DbContextOptionsBuilder<RSUPPKBDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options));
        return new(db,new Permissions(),reader,NullLogger<SensusMobileController>.Instance){ControllerContext=new(){HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"1")},"test"))}}};
    }
    private sealed class Permissions:IPermissionService
    {
        public Task<bool> IsSuperAdminAsync(ClaimsPrincipal user)=>Task.FromResult(false);
        public Task<bool> HasPermissionAsync(ClaimsPrincipal user,string code,MenuPermission permission)=>Task.FromResult(true);
        public Task<CurrentMenuPermissionDto?> GetPermissionsAsync(ClaimsPrincipal user,string code)=>Task.FromResult<CurrentMenuPermissionDto?>(null);
    }
}
