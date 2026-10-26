using AuthUtility.Api.Layanan;
using AuthUtility.Domain.Entities;

namespace AuthUtility.Tests;

public class LynRequestRulesTests
{
    /* ---------- Prioritas (mengikuti form: Normal, Sedang, Tinggi, Urgent) ---------- */

    [Fact]
    public void Priority_AllFollowsForm()
    {
        Assert.Equal(new[] { "NORMAL", "SEDANG", "TINGGI", "URGENT" }, LynPriority.All);
    }

    [Fact]
    public void Priority_RankUrgentFirst_LegacyValuesStillRanked()
    {
        Assert.True(LynPriority.Rank("URGENT") < LynPriority.Rank("TINGGI"));
        Assert.True(LynPriority.Rank("TINGGI") < LynPriority.Rank("SEDANG"));
        Assert.True(LynPriority.Rank("SEDANG") < LynPriority.Rank("NORMAL"));
        Assert.Equal(LynPriority.Rank("URGENT"), LynPriority.Rank("MENDESAK"));
        Assert.Equal(LynPriority.Rank("NORMAL"), LynPriority.Rank("RENDAH"));
        Assert.True(LynPriority.Rank("???") > LynPriority.Rank("NORMAL"));
    }

    /* ---------- Judul otomatis ---------- */

    [Fact]
    public void BuildTitle_ServiceAndLocation()
    {
        Assert.Equal("Pembuatan Peta - persil 17-01", LynRequestRules.BuildTitle("Pembuatan Peta", null, " persil 17-01 "));
    }

    [Fact]
    public void BuildTitle_UsesJenisLainnyaWhenFilled()
    {
        Assert.Equal("Peta kontur - Wilayah 2", LynRequestRules.BuildTitle("Lainnya", " Peta kontur ", "Wilayah 2"));
    }

    [Fact]
    public void BuildTitle_WithoutLocation()
    {
        Assert.Equal("Survey Lapangan", LynRequestRules.BuildTitle("Survey Lapangan", null, "  "));
    }

    [Fact]
    public void BuildTitle_MaxLength200()
    {
        var title = LynRequestRules.BuildTitle("Pembuatan Peta", null, new string('x', 300));
        Assert.Equal(200, title.Length);
    }

    /* ---------- Perusahaan (boleh lebih dari satu + "Yang lain") ---------- */

    [Fact]
    public void JoinCompanies_TrimsDedupsAndAppendsOther()
    {
        Assert.Equal("PT GHS; PT STI; CV Maju",
            LynRequestRules.JoinCompanies([" PT GHS", "PT STI", "", "pt ghs"], " CV Maju "));
    }

    [Fact]
    public void JoinCompanies_EmptyReturnsEmpty()
    {
        Assert.Equal("", LynRequestRules.JoinCompanies([], "  "));
    }
}
