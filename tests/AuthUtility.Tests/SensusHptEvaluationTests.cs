using AuthUtility.Api.Sensus;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Tests;

public class SensusHptEvaluationTests
{
    [Theory]
    [InlineData("ORYCTES", "TM", 3, false)]
    [InlineData("ORYCTES", "TM", 4, true)]
    [InlineData("PLESISPA", "TM", 5, false)]
    [InlineData("PLASISPA", "TM", 6, true)]
    [InlineData("ORYCTES", "TBM", 1, true)]
    [InlineData("PLESISPA", "TBM", 0, false)]
    [InlineData("TIRATHABA", "", 5, true)]
    [InlineData("RAYAP", "", 1, true)]
    [InlineData("FUSARIUM", "", 1, true)]
    public async Task OfficialThresholdBoundaries(string code, string stage, int attacked, bool expected)
    {
        var parameter = new SnsParameter { Id=1,Code="STATUS_TANAMAN",TipeData="OPTION",Opsi="TM,TBM",Active=true };
        var header = new SnsHeader {JumlahPokokSampel=100,JumlahPokokTerserang=attacked,Jenis=await Seeded(code),
            Details=new List<SnsDetail> {new(){Parameter=parameter,NilaiTeks=stage}}};
        Assert.Equal(expected,SensusHptEvaluation.Evaluate(header).PerluPengendalian);
    }

    [Theory]
    [InlineData("ULAT_API",4,false)]
    [InlineData("ULAT_API",5,true)]
    [InlineData("ULAT_KANTONG",5,true)]
    public async Task WormThresholdUsesMasterPerFrondIndicator(string code,int value,bool expected)
    {
        var header=new SnsHeader {Jenis=await Seeded(code),Details=new List<SnsDetail>{new(){Parameter=new(){Id=1,Code="JML_ULAT",Name="Jumlah ulat per pelepah",TipeData="NUMBER",Active=true,IsIndikator=true},NilaiAngka=value}}};
        Assert.Equal(expected,SensusHptEvaluation.Evaluate(header).PerluPengendalian);
    }

    [Fact]
    public async Task MissingOrMixedStageAndUnknownRuleAreNotAssumedSafe()
    {
        var header=new SnsHeader {Jenis=await Seeded("ORYCTES"),JumlahPokokSampel=100,JumlahPokokTerserang=10};
        Assert.Null(SensusHptEvaluation.Evaluate(header).PerluPengendalian);
        header.Jenis=await Seeded("GANODERMA");
        Assert.Null(SensusHptEvaluation.Evaluate(header).PerluPengendalian);
        header.Jenis=await Seeded("TIRATHABA");header.JumlahPokokSampel=0;
        Assert.Null(SensusHptEvaluation.Evaluate(header).PerluPengendalian);
    }
    private static async Task<SnsJenis> Seeded(string code)
    {
        await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var kind=new SnsJenis{Code=code,Name=code};db.SnsJenis.Add(kind);await db.SaveChangesAsync();await SensusBusinessSetup.SeedAsync(db);return kind;
    }
}
