using AuthUtility.Api.Wad;

namespace AuthUtility.Tests;

public sealed class WadMonitoringRulesTests
{
    [Theory]
    [InlineData("curah-hujan", "vw_ECSMonCurahHujan", "KodePersil")]
    [InlineData("tmak", "vw_ECSMonTmak", "KodeBlok")]
    [InlineData("tmat", "vw_ECSMonTmat", "KodePersil")]
    public void TryGetSource_UsesAnAllowlistedView(string parameter, string viewName, string positionColumn)
    {
        var found = WadMonitoringRules.TryGetSource(parameter, out var source);

        Assert.True(found);
        Assert.Equal(viewName, source.ViewName);
        Assert.Equal(positionColumn, source.PositionColumn);
    }

    [Fact]
    public void TryGetSource_RejectsUnknownParameter()
    {
        Assert.False(WadMonitoringRules.TryGetSource("dbo.Users; DROP TABLE", out _));
    }

    [Fact]
    public void BuildQuery_UsesInclusiveStartAndExclusiveEndDate()
    {
        Assert.True(WadMonitoringRules.TryGetSource("curah-hujan", out var source));

        var sql = WadMonitoringRules.BuildQuery(
            source,
            hasStartDate: true,
            hasEndDate: true,
            hasWilayah: false,
            hasPos: true);

        Assert.Contains("FROM dbo.vw_ECSMonCurahHujan", sql);
        Assert.Contains("v.Tanggal >= @startDate", sql);
        Assert.Contains("v.Tanggal < DATEADD(DAY, 1, @endDate)", sql);
        Assert.Contains("v.KodePersil = @pos", sql);
    }

    [Fact]
    public void BuildQuery_RejectsWilayahFilterWhenTheViewHasNoRegionColumn()
    {
        Assert.True(WadMonitoringRules.TryGetSource("curah-hujan", out var source));

        var error = Assert.Throws<ArgumentException>(() => WadMonitoringRules.BuildQuery(
            source,
            hasStartDate: false,
            hasEndDate: false,
            hasWilayah: true,
            hasPos: false));

        Assert.Contains("wilayah", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
