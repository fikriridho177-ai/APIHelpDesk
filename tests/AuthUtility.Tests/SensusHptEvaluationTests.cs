using AuthUtility.Api.Sensus;
using AuthUtility.Domain.Entities;

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
    public void OfficialThresholdBoundaries(string code, string stage, int attacked, bool expected)
    {
        var parameter = new SnsParameter { Id=1,Code="STATUS_TANAMAN",TipeData="OPTION",Opsi="TM,TBM",Active=true };
        var header = new SnsHeader {JumlahPokokSampel=100,JumlahPokokTerserang=attacked,Jenis=new SnsJenis {Code=code},
            Details=new List<SnsDetail> {new(){Parameter=parameter,NilaiTeks=stage}}};
        Assert.Equal(expected,SensusHptEvaluation.Evaluate(header).PerluPengendalian);
    }

    [Theory]
    [InlineData("ULAT_API",4,false)]
    [InlineData("ULAT_API",5,true)]
    [InlineData("ULAT_KANTONG",5,true)]
    public void WormThresholdUsesMasterPerFrondIndicator(string code,int value,bool expected)
    {
        var header=new SnsHeader {Jenis=new SnsJenis {Code=code},Details=new List<SnsDetail>{new(){Parameter=new(){Id=1,Code="JML_ULAT",Name="Jumlah ulat per pelepah",TipeData="NUMBER",Active=true,IsIndikator=true},NilaiAngka=value}}};
        Assert.Equal(expected,SensusHptEvaluation.Evaluate(header).PerluPengendalian);
    }

    [Fact]
    public void MissingOrMixedStageAndUnknownRuleAreNotAssumedSafe()
    {
        var header=new SnsHeader {Jenis=new SnsJenis{Code="ORYCTES"},JumlahPokokSampel=100,JumlahPokokTerserang=10};
        Assert.Null(SensusHptEvaluation.Evaluate(header).PerluPengendalian);
        header.Jenis.Code="GANODERMA";
        Assert.Null(SensusHptEvaluation.Evaluate(header).PerluPengendalian);
        header.Jenis.Code="TIRATHABA";header.JumlahPokokSampel=0;
        Assert.Null(SensusHptEvaluation.Evaluate(header).PerluPengendalian);
    }
}
