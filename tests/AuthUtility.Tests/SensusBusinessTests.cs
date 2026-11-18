using System.Security.Claims;
using System.Text.Json;
using AuthUtility.Api.Controllers;
using AuthUtility.Api.Sensus;
using AuthUtility.Application.DTOs.Security;
using AuthUtility.Application.DTOs.Sensus;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuthUtility.Tests;

public class SensusBusinessTests
{
    private static AppDbContext Db()=>new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static SnsHptRule Rule(string parameter="PERSEN_TERSERANG",string op=">=",decimal threshold=5)=>new(){Id=1,ParameterCode=parameter,Operator=op,ThresholdValue=threshold,Message="Lakukan tindakan dari master.",ElseMessage="Monitoring dari master.",Active=true};
    private static SnsHeader Header(long id=1,int kind=1,int attacked=5,int samples=100,string land="ORGANIK",string status="APPROVED")=>new(){Id=id,JenisId=kind,Status=status,JenisForm="FORM1",StatusLahan=land,PersilSumber="KLP",PersilId="123",PersilKode="P-123",KodeWilayah="W-BARU",KodeAfdeling="A",TanggalSensus=new(2026,11,10),JumlahPokokSampel=samples,JumlahPokokTerserang=attacked,
        LuasHa=10,JumlahPokokPersil=1500,PetugasUserId=1,ApprovedByUserId=2,ApprovedAt=new(2026,11,12,0,0,0,DateTimeKind.Utc),Jenis=new(){Id=kind,Code="ADMIN_NEW_TYPE",Name="Jenis baru",Rules=new List<SnsHptRule>{Rule()}}};
    private static SensusBusinessFilter Filter()=>new(){DateFrom=new(2026,11,1),DateTo=new(2026,11,30)};
    private static readonly List<SnsReportKind> Kinds=new(){new(1,"NEW","Jenis baru","HAMA"),new(2,"OTHER","Lainnya","PENYAKIT")};

    [Theory]
    [InlineData(">",5,false)][InlineData(">=",5,true)][InlineData("<",4,true)][InlineData("<=",5,true)][InlineData("=",5,true)]
    public void NewMasterTypeAndWhitelistOperatorsNeedNoHptCodeBranch(string op,int attacked,bool expected)
    {
        var h=Header(attacked:attacked);h.Jenis!.Rules=new List<SnsHptRule>{Rule(op:op)};
        var result=SensusHptEvaluation.Evaluate(h);Assert.Equal(expected,result.PerluPengendalian);Assert.Equal(expected?"red":"green",result.Color);
        Assert.Equal(expected?"Lakukan tindakan dari master.":"Monitoring dari master.",result.Message);
    }
    [Fact]
    public void MissingConditionInactiveRulesAndInvalidOperatorsDoNotBecomeGreen()
    {
        var h=Header();var r=h.Jenis!.Rules.Single();r.ConditionParameterCode="FASE_TANAMAN";r.ConditionOperator="=";r.ConditionValue="TM";
        Assert.Null(SensusHptEvaluation.Evaluate(h).PerluPengendalian);h.FaseTanaman="TM";Assert.True(SensusHptEvaluation.Evaluate(h).PerluPengendalian);
        r.Operator="!= ";Assert.Null(SensusHptEvaluation.Evaluate(h).PerluPengendalian);r.Operator=">=";r.Active=false;Assert.Equal("NONE",SensusHptEvaluation.Evaluate(h).EvaluationType);
    }
    [Fact]
    public void DynamicParameterAggregationAndConditionAreGeneric()
    {
        var h=Header();h.Details=new List<SnsDetail>{new(){Parameter=new(){Code="EGGS",TipeData="NUMBER",Active=true},NilaiAngka=2},new(){Parameter=new(){Code="EGGS",TipeData="NUMBER",Active=true},NilaiAngka=8}};
        var r=Rule("EGGS",">=",6);h.Jenis!.Rules=new List<SnsHptRule>{r};Assert.False(SensusHptEvaluation.Evaluate(h).PerluPengendalian);
        r.Aggregation="SUM";Assert.True(SensusHptEvaluation.Evaluate(h).PerluPengendalian);r.ConditionParameterCode="POKOK_SAMPEL";r.ConditionOperator=">";r.ConditionValue="100";Assert.Null(SensusHptEvaluation.Evaluate(h).PerluPengendalian);
    }
    [Fact]
    public async Task SeedIsIdempotentPreservesAdminRulesAndDoesNotInventUnknownRules()
    {
        await using var db=Db();var p=new SnsJenis{Code="PLESISPA",Name="Plesispa"};var u=new SnsJenis{Code="ULAT_API",Name="Ulat Api"};var unknown=new SnsJenis{Code="UNLISTED",Name="Unlisted"};
        var custom=new SnsJenis{Code="ORYCTES",Name="Oryctes",Rules=new List<SnsHptRule>{Rule(threshold:99)}};db.SnsJenis.AddRange(p,u,unknown,custom);await db.SaveChangesAsync();await SensusBusinessSetup.SeedAsync(db);
        var count=await db.SnsHptRules.CountAsync();await SensusBusinessSetup.SeedAsync(db);Assert.Equal(count,await db.SnsHptRules.CountAsync());Assert.Empty(unknown.Rules);Assert.Equal(99,custom.Rules.Single().ThresholdValue);
        Assert.True(p.Form2Enabled);Assert.True(u.Form2Enabled);Assert.Contains(u.Parameters,x=>x.Code=="ULAT_KECIL"&&x.FormScope=="FORM2");Assert.Contains(u.Parameters,x=>x.Code=="JML_ULAT"&&x.FormScope=="ALL");
        Assert.False(SensusBusinessContext.ParameterFor(u.Parameters.Single(p=>p.Code=="ULAT_KECIL"),"FORM1"));
    }
    [Fact]
    public async Task LegacyAliasesByNameAreMigratedButNewAdminTypesDoNotReceiveAutomaticRules()
    {
        await using var db=Db();var legacy=new SnsJenis{Code="HPT_OLD_01",Name="Sensus Kumbang Tanduk"};var added=new SnsJenis{Code="TIRATHABA",Name="Tirathaba",BusinessSeedVersion=1};
        db.SnsJenis.AddRange(legacy,added);await db.SaveChangesAsync();await SensusBusinessSetup.SeedAsync(db);Assert.Equal(2,legacy.Rules.Count);Assert.Empty(added.Rules);
    }
    [Fact]
    public void PersilStatusOverridesClientAndUnknownLandIsNeverAssumedNonOrganic()
    {
        var source=new Dictionary<string,object?>{{"StatusLahan","O"},{"Populasi",1250},{"LuasHa",12.5m},{"StatusTanaman","TM"}};
        var metadata=SensusBusinessContext.FromPersil(source);var h=Header();SensusBusinessContext.Apply(h,new(){StatusLahan="NON_ORGANIK",LuasHa=1},metadata);
        Assert.Equal("ORGANIK",h.StatusLahan);Assert.Equal("PERSIL",h.StatusLahanSumber);Assert.Equal(12.5m,h.LuasHa);Assert.Equal(1250,h.JumlahPokokPersil);
        Assert.Null(SensusBusinessContext.FromPersil(new Dictionary<string,object?>()).StatusLahan);Assert.Equal("KONVERSI",SensusBusinessContext.NormalizeLand("konversi"));
        Assert.NotNull(SensusBusinessContext.Validate(new(){JenisForm="FORM2"},new SnsJenis(),"FORM2"));
    }
    [Fact]
    public void RevisionCanClearContextWhileLegacyClientOmissionPreservesSnapshot()
    {
        var h=Header();h.Cuaca="Cerah";
        SensusBusinessContext.Apply(h,null,null);Assert.Equal("ORGANIK",h.StatusLahan);Assert.Equal("Cerah",h.Cuaca);
        SensusBusinessContext.Apply(h,new(){JenisForm="FORM1"},null);Assert.Null(h.StatusLahan);Assert.Null(h.StatusLahanSumber);Assert.Null(h.Cuaca);Assert.Null(h.LuasHa);
    }
    [Fact]
    public void SchemaIsEmbeddedWithIdempotentGuardsAndDoesNotCreateTransactionTables()
    {
        using var resource=typeof(SensusBusinessSetup).Assembly.GetManifestResourceStream("Sensus.BusinessSchema.sql");Assert.NotNull(resource);
        using var reader=new StreamReader(resource!);var sql=reader.ReadToEnd();Assert.Contains("ADD COLUMN IF NOT EXISTS",sql);Assert.Contains("CREATE TABLE IF NOT EXISTS \"MstHptRule\"",sql);Assert.Contains("pg_advisory_xact_lock",sql);
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS \"Sns",sql);Assert.DoesNotContain("UtlMenu",sql);Assert.DoesNotContain("Qc",sql);
    }
    [Fact]
    public void ReportsExcludeUnapprovedDeduplicateRechecksAndPreserveEachHptDenominator()
    {
        var old=Header(1,attacked:1);var latest=Header(2);var other=Header(3,2,attacked:20,samples:200);var unapproved=Header(4,status:"SUBMITTED");var unknown=Header(5,land:"");unknown.PersilId="UNKNOWN";
        var report=SensusBusinessReportQuery.Build(new[]{old,latest,other,unapproved,unknown},Kinds,Filter());
        Assert.Equal(4,report.ApprovedBeforeDedup);Assert.Equal(3,report.Observations.Count);Assert.DoesNotContain(report.Observations,x=>x.Id==1||x.Id==4);
        var row=report.Rekap.Single(x=>x.PersilId=="123");Assert.Null(row.Sampel);Assert.Equal(1500,row.Populasi);Assert.Equal(10,row.LuasHa);Assert.NotEmpty(row.Notes);
        Assert.Equal(new[]{100,200},row.Hpt.Select(x=>x.Sampel));Assert.Equal(2,report.Analysis.PerStatusLahan.Count);Assert.Contains(report.Analysis.PerStatusLahan,x=>x.Key=="BELUM_DIKETAHUI");
        Assert.All(report.Memo,x=>Assert.Contains("master",x.Recommendation));
    }
    [Fact]
    public void AllBusinessFiltersWorkForAnyRegionAndFormAndLand()
    {
        var a=Header();var b=Header(2,land:"NON_ORGANIK");b.JenisForm="FORM2";var c=Header(3);c.KodeWilayah="KIN";
        var filter=Filter();filter.Wilayah="W-BARU";filter.StatusLahan="N";filter.JenisForm="FORM2";filter.JenisId=1;
        var report=SensusBusinessReportQuery.Build(new[]{a,b,c},Kinds,filter);Assert.Equal(2,Assert.Single(report.Observations).Id);
        filter.DateTo=filter.DateFrom!.Value.AddDays(366);Assert.NotNull(SensusBusinessReportQuery.Validate(filter));
    }
    [Theory]
    [InlineData(false,false)][InlineData(true,false)][InlineData(true,true)]
    public async Task BusinessReadAndExportEnforceSeparateExistingPermissions(bool read,bool export)
    {
        await using var db=Db();var permissions=new Permissions(read,export);
        var controller=new SensusController(db,permissions,new PersilReader(new MyPADDbContext(new DbContextOptionsBuilder<MyPADDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options),new RSUPPKBDbContext(new DbContextOptionsBuilder<RSUPPKBDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)),NullLogger<SensusController>.Instance);
        Assert.Equal(read,await controller.BusinessReport(Filter(),default) is OkObjectResult);Assert.Equal(read&&export,await controller.BusinessExport(Filter(),default) is OkObjectResult);
    }
    [Fact]
    public async Task MasterRuleCrudValidatesWhitelistAndUsesExistingMasterUpdatePermission()
    {
        await using var db=Db();db.SnsJenis.Add(new(){Id=1,Code="DYNAMIC",Name="Jenis admin"});await db.SaveChangesAsync();
        var forbidden=new SensusMasterController(db,new Permissions(true,false));
        var body=new SaveSnsRuleRequest{ParameterCode="PERSEN_TERSERANG",Operator=">=",ThresholdValue=5,Message="Rekomendasi bisnis",ElseMessage="Monitoring bisnis"};
        Assert.IsType<ForbidResult>(await forbidden.CreateRule(1,body));
        var controller=new SensusMasterController(db,new Permissions(true,false,true));body.Operator="OR 1=1";Assert.IsType<BadRequestObjectResult>(await controller.CreateRule(1,body));
        body.Operator=">=";body.ParameterCode="MISSING_PARAMETER";Assert.IsType<BadRequestObjectResult>(await controller.CreateRule(1,body));
        body.ParameterCode="PERSEN_TERSERANG";var created=Assert.IsType<OkObjectResult>(await controller.CreateRule(1,body));var rule=Assert.IsType<SnsRuleDto>(created.Value);
        body.ThresholdValue=6;Assert.IsType<OkObjectResult>(await controller.UpdateRule(rule.Id,body));Assert.Equal(6,(await db.SnsHptRules.SingleAsync()).ThresholdValue);
        Assert.IsType<NoContentResult>(await controller.DisableRule(rule.Id));Assert.False((await db.SnsHptRules.SingleAsync()).Active);Assert.Single((await SensusMasterController.QueryJenis(db,false)).Single().Rules!);
    }
    [Fact]
    public async Task SaveRuleAcceptsEmptyOptionalFormAndCanonicalizesTextWithoutChangingThreshold()
    {
        await using var db=Db();db.SnsJenis.Add(new(){Id=1,Code="NEW",Name="Jenis admin"});await db.SaveChangesAsync();
        var controller=new SensusMasterController(db,new Permissions(true,false,true));
        var body=new SaveSnsRuleRequest{ParameterCode=" persen_terserang ",Operator=" >= ",Aggregation="avg",ThresholdValue=5,ThresholdUnit=" % ",JenisForm="",StatusLahan=" ",ConditionParameterCode="",ConditionOperator="",ConditionValue="",ResultCode="perlu_pengendalian",Color="RED",ElseColor="GREEN",Message=" Kendalikan. ",ElseMessage=" Monitoring. "};
        var response=Assert.IsType<OkObjectResult>(await controller.CreateRule(1,body));var saved=Assert.IsType<SnsRuleDto>(response.Value);
        Assert.Equal(5,saved.ThresholdValue);Assert.Equal(">=",saved.Operator);Assert.Equal("PERSEN_TERSERANG",saved.ParameterCode);Assert.Equal("red",saved.Color);Assert.Null(saved.JenisForm);Assert.Null(saved.ConditionParameterCode);
        body.JenisForm=" form2 ";Assert.IsType<OkObjectResult>(await controller.UpdateRule(saved.Id,body));Assert.Equal("FORM2",(await db.SnsHptRules.SingleAsync()).JenisForm);
        body.ConditionParameterCode="PERSEN_TERSERANG";body.ConditionOperator=">=";body.ConditionValue="1,5";
        Assert.IsType<OkObjectResult>(await controller.UpdateRule(saved.Id,body));Assert.Equal("1.5",(await db.SnsHptRules.SingleAsync()).ConditionValue);
        body.Operator="!=";Assert.IsType<BadRequestObjectResult>(await controller.UpdateRule(saved.Id,body));
    }
    [Fact]
    public async Task ApprovedQueryMatchesPureProjectionAndProvidesFixtureForFrontendExports()
    {
        await using var db=Db();db.Users.AddRange(new UtlUser{Id=1,UserId="petugas",Name="Petugas nyata",PasswordHash="test",GroupId=1},new UtlUser{Id=2,UserId="approver",Name="Approver nyata",PasswordHash="test",GroupId=1});
        var a=Header();a.Jenis!.ReportGroup="HAMA";a.Jenis.Form2Enabled=true;a.JenisForm="FORM2";a.Jenis.Parameters.Add(new(){Id=11,JenisId=1,Code="JML_ULAT",Name="Ulat per pelepah",TipeData="NUMBER",Active=true,FormScope="ALL"});a.Details.Add(new(){ParameterId=11,NoPokok=1,NilaiAngka=5});
        db.SnsHeaders.Add(a);db.SnsHeaders.Add(new(){Id=2,JenisId=1,PetugasUserId=1,PersilId="REJECT",Status="REJECTED",TanggalSensus=new(2026,11,10)});await db.SaveChangesAsync();
        var report=await SensusBusinessReportQuery.ReadAsync(db,Filter());Assert.NotNull(report);Assert.Single(report!.Observations);Assert.Equal("Petugas nyata",report.Observations[0].Petugas);
        var path=Path.Combine(Path.GetTempPath(),"sensus-business-report-fixture.json");await File.WriteAllTextAsync(path,JsonSerializer.Serialize(report,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
    private sealed class Permissions(bool read,bool export,bool update=false):IPermissionService
    {
        public Task<bool> IsSuperAdminAsync(ClaimsPrincipal u)=>Task.FromResult(false);
        public Task<bool> HasPermissionAsync(ClaimsPrincipal u,string code,MenuPermission p)=>Task.FromResult(p==MenuPermission.Read?read:p==MenuPermission.Export?export:p==MenuPermission.Update&&update);
        public Task<CurrentMenuPermissionDto?> GetPermissionsAsync(ClaimsPrincipal u,string code)=>Task.FromResult<CurrentMenuPermissionDto?>(null);
    }
}
