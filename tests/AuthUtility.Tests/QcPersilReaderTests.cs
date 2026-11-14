using AuthUtility.Api.Sensus;

namespace AuthUtility.Tests;

public class QcPersilReaderTests
{
    [Theory]
    [InlineData("NANAS", "vwPersilNNS")]
    [InlineData("KELAPA", "vwMstPersilKelapa")]
    public void QcSourceUsesRequiredMyPadView(string commodity, string view)
    {
        var source = PersilReader.QcSource(commodity);
        Assert.Equal("MyPAD", source.Database);
        Assert.Equal(view, source.Table);
    }

    [Fact]
    public void QcSourceRejectsUntrustedViewNames() =>
        Assert.Throws<ArgumentException>(() => PersilReader.QcSource("anything; DROP TABLE x"));

    [Fact]
    public void QcMappingKeepsPersilAndSubpersilSeparate()
    {
        var row = PersilReader.MapQcPersil(new Dictionary<string, object?>
        {
            ["PersilID"] = 123, ["KodePersil"] = " P01 ", ["KodeSubPersil"] = " P01A ",
            ["KodeWilayah"] = " W01 ", ["KodeAfdeling"] = " A01 "
        });
        Assert.Equal("123", row.PersilId);
        Assert.Equal("P01", row.PersilKode);
        Assert.Equal("P01A", row.Subpersil);
        Assert.Equal("W01", row.Wilayah);
        Assert.Equal("A01", row.Afdeling);
    }

    [Fact]
    public void QcMappingSupportsViewAliasesAndMissingOptionalValues()
    {
        var row = PersilReader.MapQcPersil(new Dictionary<string, object?>
        {
            ["persil"] = " N01 ", ["subpersil"] = " N01-2 ", ["wilayah"] = " II ",
            ["afdeling"] = DBNull.Value
        });
        Assert.Equal("N01", row.PersilKode);
        Assert.Equal("N01-2", row.Subpersil);
        Assert.Equal("II", row.Wilayah);
        Assert.Null(row.Afdeling);
        Assert.False(string.IsNullOrEmpty(row.PersilId));
    }
}
