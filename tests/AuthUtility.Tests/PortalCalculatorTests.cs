using AuthUtility.Api.Layanan;
using AuthUtility.Domain.Entities;

namespace AuthUtility.Tests;

public class PortalCalculatorTests
{
    private const int Me = 1;
    private const int Other = 2;
    private static readonly DateTime Now = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

    private static LynRequest Req(long id, string status, int requester = Other, string priority = "NORMAL",
        DateTime? created = null, DateTime? closed = null) => new()
    {
        Id = id,
        Status = status,
        RequesterUserId = requester,
        Priority = priority,
        CreatedAt = created ?? Now.AddDays(-1),
        ClosedAt = closed
    };

    private static QueueCandidate C(LynRequest r, params string[] actions) => new(r, actions);

    /* ---------- SelectQueue ---------- */

    [Fact]
    public void SelectQueue_ExcludesOwnRequests()
    {
        var result = PortalCalculator.SelectQueue(
            [C(Req(1, LynStatus.Baru, requester: Me), "ACCEPT"), C(Req(2, LynStatus.Baru), "ACCEPT")], Me, 10);

        Assert.Equal(1, result.Total);
        Assert.Equal(2, Assert.Single(result.Items).Id);
    }

    [Fact]
    public void SelectQueue_OnlyNextStepStatuses()
    {
        var result = PortalCalculator.SelectQueue(
        [
            C(Req(1, LynStatus.Baru), "ACCEPT", "REJECT"),
            C(Req(2, LynStatus.Baru), "COMMENT"),
            C(Req(3, LynStatus.Diterima), "SCHEDULE"),
            C(Req(4, LynStatus.Dijadwalkan), "PROCESS"),
            C(Req(5, LynStatus.MenungguInfo), "PROCESS"),
            C(Req(6, LynStatus.Diproses), "FINISH"),
        ], Me, 10);

        Assert.Equal(new long[] { 1, 3, 4 }, result.Items.Select(x => x.Id).OrderBy(x => x));
    }

    [Fact]
    public void SelectQueue_SortsByPriorityThenOldest()
    {
        var result = PortalCalculator.SelectQueue(
        [
            C(Req(1, LynStatus.Baru, priority: "SEDANG", created: Now.AddDays(-5)), "ACCEPT"),
            C(Req(2, LynStatus.Baru, priority: "URGENT", created: Now.AddDays(-1)), "ACCEPT"),
            C(Req(3, LynStatus.Baru, priority: "SEDANG", created: Now.AddDays(-9)), "ACCEPT"),
            C(Req(4, LynStatus.Baru, priority: "NORMAL", created: Now.AddDays(-20)), "ACCEPT"),
            C(Req(5, LynStatus.Baru, priority: "TINGGI", created: Now.AddDays(-1)), "ACCEPT"),
        ], Me, 10);

        Assert.Equal(new long[] { 2, 5, 3, 1, 4 }, result.Items.Select(x => x.Id));
    }

    [Fact]
    public void SelectQueue_TakeLimitsItemsButNotTotal()
    {
        var candidates = Enumerable.Range(1, 5).Select(i => C(Req(i, LynStatus.Baru), "ACCEPT"));

        var result = PortalCalculator.SelectQueue(candidates, Me, 2);

        Assert.Equal(5, result.Total);
        Assert.Equal(2, result.Items.Count);
    }

    /* ---------- ComputeKpi ---------- */

    private static readonly DateTime From = Now.AddDays(-30);

    [Fact]
    public void ComputeKpi_FiltersByFromDate()
    {
        var result = PortalCalculator.ComputeKpi(
            [(Req(1, LynStatus.Baru, created: Now.AddDays(-31)), "GIS"), (Req(2, LynStatus.Baru), "GIS")], From);

        Assert.Equal(1, Assert.Single(result).Masuk);
    }

    [Fact]
    public void ComputeKpi_GroupsAndCounts()
    {
        var result = PortalCalculator.ComputeKpi(
        [
            (Req(1, LynStatus.Baru), "GIS"),
            (Req(2, LynStatus.Diproses), "GIS"),
            (Req(3, LynStatus.Selesai, closed: Now), "GIS"),
            (Req(4, LynStatus.Ditolak, closed: Now), "GIS"),
            (Req(5, LynStatus.Baru), "QC"),
        ], From);

        var gis = result.Single(x => x.Division == "GIS");
        Assert.Equal((4, 2, 1), (gis.Masuk, gis.Terbuka, gis.Selesai));
        Assert.Equal(new[] { "GIS", "QC" }, result.Select(x => x.Division));
    }

    [Fact]
    public void ComputeKpi_AverageDaysRoundedOneDecimal()
    {
        var created = Now.AddDays(-10);
        var result = PortalCalculator.ComputeKpi(
        [
            (Req(1, LynStatus.Selesai, created: created, closed: created.AddDays(1)), "GIS"),
            (Req(2, LynStatus.Selesai, created: created, closed: created.AddDays(2.5)), "GIS"),
        ], From);

        Assert.Equal(1.8, Assert.Single(result).RataHariSelesai);
    }

    [Fact]
    public void ComputeKpi_NoFinishedGivesNullAverage()
    {
        var result = PortalCalculator.ComputeKpi([(Req(1, LynStatus.Baru), "GIS")], From);

        Assert.Null(Assert.Single(result).RataHariSelesai);
    }

    [Fact]
    public void ComputeKpi_IgnoresFinishedWithoutClosedAt()
    {
        var created = Now.AddDays(-10);
        var result = PortalCalculator.ComputeKpi(
        [
            (Req(1, LynStatus.Selesai, created: created, closed: created.AddDays(2)), "GIS"),
            (Req(2, LynStatus.Selesai, created: created, closed: null), "GIS"),
        ], From);

        var gis = Assert.Single(result);
        Assert.Equal(2, gis.Selesai);
        Assert.Equal(2.0, gis.RataHariSelesai);
    }

    [Fact]
    public void ComputeKpi_NullDivisionBecomesLainnya()
    {
        var result = PortalCalculator.ComputeKpi([(Req(1, LynStatus.Baru), null)], From);

        Assert.Equal(PortalCalculator.OtherDivision, Assert.Single(result).Division);
        Assert.Equal("Lainnya", PortalCalculator.OtherDivision);
    }
}
